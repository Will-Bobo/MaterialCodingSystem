# MCS V1.4（历史编码兼容版）技术实现设计报告

> 基于当前仓库代码与 V1.4 PRD 增量要求，给出最小实现方案与分层落点。  
> 范围：**主物料新增能力**（自动编码 + 历史编码录入）与 **分类起始流水号**（`Category.start_serial_no`）。  
> 不在本报告范围：suffix 规则、spec/description/spec_normalized 定义、LIKE 搜索、V1/V2 边界、数据库备份/恢复（已在 Phase5 完成）。

（PATCHED V3）现实对齐声明（避免入口模型混淆）：

- **“替代料创建（Replacement）”是现有功能入口**：系统当前以 **`group_id + spec + description`** 创建替代料；UI 也以既有替代料页面/流程为准。
- **“历史编码补录（ManualExistingCode）”是 V1.4 新增入口**：允许输入完整编码 `existing_code`（含 suffix=A 或 B–Z），其本质是“补录历史 item”，并不等同于“替代料创建页面支持输入编码”。
- 因此本文档中凡涉及 `existing_code` 的路径，均指 **V1.4 的历史补录入口**；凡涉及 `create_material_replacement` 的路径，均指 **现有 replacement 用例入口（group_id）**。

## （PATCHED V6）系统级最终 Invariants（最小实现可落地）

- **group 创建权（收敛）**：仅 **Auto** 与 **ManualExistingCode（suffix='A' 且 group 不存在）** 允许创建 `material_group`；**Replacement 禁止创建 group**（只能复用 `group_id`）
- **serial_no 唯一来源（强制 invariant）**：业务序列号唯一来源为 `material_group.serial_no`；所有 `maxSerialNo`/排序键/分组键的计算必须基于 `category_id + material_group.serial_no`（禁止用 `category_code` 做 max/排序查询维度）
- **anchor item（A）语义（从强 invariant 降级为可治理项）**：
  - `suffix='A'` 是业务上的 **anchor item（推荐存在）**
  - Auto 创建必须在同一事务内提交 `group + item(A)`
  - Manual 在 `suffix='A'` 且 group 不存在时，必须在同一事务内提交 `group + item(A)`
  - 若发现历史 group 无 A：不作为系统级致命 invariant；记为**数据巡检告警项（warning）**
  - Replacement / Manual `suffix!='A'` 遇到“无 A 的 group”时：**阻断写入并提示修复数据**

（PATCHED V6）排序说明（避免过度约束现有 UI）：

- suffix **不代表创建时间顺序**；但展示排序可按业务场景决定：
  - **编码展示/替代料列表**：按 suffix（A–Z）排序可接受
  - **操作日志/审计**：按 `id`（或实现中已有的时间字段）排序

---

## 1. 目标与冻结约束

### 1.1 V1.4 新增目标

- **两种建料方式**
  - **自动生成物料编码**（默认）
  - **输入已有历史物料编码**（兼容旧料号）
- **分类级起始流水号**
  - `Category.start_serial_no`：用于约束自动编码生成的起点，避免占用历史编码区间

### 1.2 必须保持不变的既有规则（继承 V1.3 冻结口径）

- `spec / description / spec_normalized` 定义与行为不变
- `UNIQUE(category_code, spec)`、LIKE 搜索口径不变
- （PATCHED V4）suffix 规则最终口径：**不要求连续（gap allowed）**；保留 A–Z 上限与并发重试机制；`UNIQUE(group_id, suffix)` 仍是唯一性约束
- 编码/后缀唯一性：`UNIQUE(code)`、`UNIQUE(group_id, suffix)` **包含废弃数据**（废弃不释放）
- Gate/PathProvider/运维互斥不变（与本功能无直接耦合）

---

## 2. 数据库与迁移设计（SQLite）

### 2.1 新增字段

在 `category` 表新增：

- `start_serial_no INTEGER NOT NULL DEFAULT 1`

约束语义：

- 必填
- `>= 1`
- 创建分类时录入
- 可修改（仅管理员）

### 2.2 迁移策略

- 通过一次性 DDL 迁移添加字段并设默认值：
  - 旧数据 `start_serial_no` 默认为 1（即旧行为兼容）
- 如需更严格数据质量：
  - Phase2 再补充 `CHECK(start_serial_no >= 1)`（V1.4 可选，当前以 PRD 为准）

---

## 3. 自动编码生成规则升级（V1.4）

### 3.1 规则定义

自动生成主物料编码（A）时：

\[
next\_serial\_no = \max(\text{maxSerialNo},\ start\_serial\_no - 1) + 1
\]

说明：

- 首次自动生成不得小于 `start_serial_no`
- 若已有更大流水号则继续递增
- 自动流水号永不回退（手工录入较小历史编码不影响）

（PATCHED V7）start_serial_no 修改后的冻结口径（必须明确）

- `start_serial_no` 一旦被修改，**立即影响后续 Auto 编码生成**
- 生成公式保持不变：
  - `next_serial_no = max(maxSerialNo, start_serial_no - 1) + 1`
- 允许跳号；不回填历史区间；不影响既有编码（已生成/已补录的不改）

### 3.2 并发语义

- 仍由 `UNIQUE(category_id, serial_no)` + 事务级重试兜底（最多 3 次）
- 该规则不改变并发模型，仅改变“计算 next 值”的输入来源（增加 `start_serial_no`）

（PATCHED V6）并发重试适用范围收敛（重要）

- **仅 Auto 模式**：生成 `next_serial_no` 时允许最多 3 次重试（并发冲突兜底）
- **ManualExistingCode 模式**：用户指定完整编码与 suffix
  - `UNIQUE(code)` / `UNIQUE(group_id, suffix)` 冲突时 **直接失败**
  - **不得**自动改号/改 suffix 重试（避免“用户输入被系统改写”）

---

## 4. 历史物料编码录入模式（V1.4）

### 4.1 输入格式

示例：`ZDA0000123A`

解析输出：

- `category_code = ZDA`
- `serial_no = 123`
- `suffix = A`

### 4.2 校验清单（必须）

1. 编码格式合法（`[分类编码][7位数字][A-Z]`）
2. `category_code` 必须存在
3. `code` 全局唯一（已存在 → 禁止创建）
4. `suffix` 合法（A-Z）
5. `serial_no >= 1`

通过后允许补录记录（见 **6.2（PATCHED）** 的“先查 group 再插入”的约束）。

### 4.3 与自动编码共存规则

- 手工录入历史编码不会触发自动流水号回退
- 后续自动编码仍按 **max(maxSerialNo, start_serial_no-1)+1** 生成

### 4.4 （PATCHED）手工录入（manual）模式的本质与关系语义（必须补齐）

- **material_group = serial_no 维度**：同一 `category_id + serial_no` 对应同一个替代组（group 粒度必须稳定、不可被重复创建）。
- **material_item = suffix 维度**：同一 group 下通过 `suffix(A–Z)` 扩展多个物料行，唯一性由 `UNIQUE(group_id, suffix)` 保证。
- **manual 模式本质**：**补录历史 group + item**，并复用现有编码体系；不是创建新的编码体系分支（不引入第三套编码规则）。

### 4.5 （PATCHED V2）替代料录入语义规则（suffix≠A）与 group 依赖约束

#### 4.5.1 规则定义（业务语义）

- group 定义不变：`material_group = category_id + serial_no`
- `suffix='A'` 表示该 group 的主料项（主物料行）
- `suffix!='A'`（B–Z）表示该 group 的替代料项（同 serial_no 下的扩展）

（PATCHED V6）补充：suffix 语义与排序口径（收敛为可落地表述）

- suffix **不代表创建时间顺序**，仅表示唯一性维度（A–Z）
- 展示排序按业务场景选择：
  - 编码展示/替代料列表：按 suffix（A–Z）排序可接受
  - 操作日志/审计：按 `id`（或实现中已有的时间字段）排序

#### 4.5.2 强约束：替代料 group 必须存在依赖（禁止“无主料造组”）

当 **历史编码补录入口**（`manual_existing_code`）且 `suffix != 'A'`（即 B–Z 替代料项）时：

1) 必须先按 `category_id + serial_no` 查询 group 是否存在  
2) **IF group 不存在：禁止创建**（不得创建 group / 不得插入 item），并返回：
   - `VALIDATION_ERROR`
   - message 规范（固定模板）：
     - `"不存在 {code_with_suffix=A} 主料，替代料号不能创建"`
   - 示例：
     - 输入：`ZDA0000123B`
     - 若 `ZDA0000123A` 不存在 → 拒绝
3) **IF group 存在：允许创建 suffix=B–Z 的 item**（受 `UNIQUE(group_id, suffix)` 约束）

#### 4.5.3 替代料允许“断号创建”（suffix 不要求连续）

- suffix **不要求连续**
- 只要 group 存在即可新增任意 `A–Z` suffix（仍受唯一性约束）
- 示例：
  - 已存在：`ZDA0000123A`、`ZDA0000123C`
  - 允许新增：`ZDA0000123B`

（PATCHED V3）与现有 Replacement 功能的关系（必须澄清）：

- 上述规则描述的是 **V1.4 历史补录入口**补录 `suffix=B–Z` 的合法性约束；它不会改变现有 Replacement 页面仍以 `group_id` 为入口的事实。
- 现有 Replacement 用例不接收 `suffix` / `code` 作为输入，suffix 分配由系统内部完成；但其业务语义仍需满足“替代料依赖已存在 group”的一致性约束。

---

## 5. 分层落点（按现有架构最小改动）

### 5.1 Domain（纯规则）

建议新增/扩展：

- `Domain/Services/CodeGenerator`（已存在）：扩展一个“历史编码解析器”
  - `TryParseItemCode(string code) -> (category_code, serial_no, suffix)` 或 Result/错误码
  - 仅做格式与拆分，不做 DB 校验

约束：

- Domain 不做 IO，不查 DB

### 5.2 Application（用例编排与事务）

改动点集中在 **历史补录与主料创建入口**（如 `MaterialApplicationService.CreateMaterialItemA` 或其等价用例）：

- 新增输入字段以表达编码方式：
  - `code_mode: auto | manual_existing_code`
  - `existing_code`（仅 manual 模式使用）
- 分支编排：
  - **Auto 模式**：按 V1.4 规则计算 serial_no 后走现有 “InsertGroup + InsertItem(A)” 流程
  - **Manual 模式（PATCHED）**：
    1) **Normalize 输入**：`existing_code = Trim + Uppercase`
    2) 解析 `existing_code` → `(category_code, serial_no, suffix)`（suffix **支持 A–Z 全范围**）
    3) **强校验**：解析得到的 `category_code` 必须与“当前选择分类”一致（不一致 → `VALIDATION_ERROR` 字段级错误）
    4) 校验 `category_code` 存在（不存在 → `CATEGORY_NOT_FOUND`）
    5) 校验 `serial_no` 为整数且 `>= 1`（否则 → `VALIDATION_ERROR`）
    6) 校验 `suffix` 在 `A–Z`（否则 → `VALIDATION_ERROR`）
    7) **查询 group 是否存在（关键修正）**：
       - 按 **`category_id + serial_no`** 查询 `material_group`
       - IF exists：**仅新增 `material_item`**（suffix 追加）
       - ELSE：
         - IF `suffix == 'A'`：创建 `material_group` → 再创建 `material_item`
         - IF `suffix != 'A'`：**禁止自动补 group**，返回 `VALIDATION_ERROR`（见 4.5.2 / 5.2 PATCHED V2 口径）
    8) `code` 全局唯一：以 DB 强约束 `UNIQUE(code)` 为准；允许业务提前校验，但最终以 DB 约束裁决

（PATCHED V2）Manual 模式流程细化（替代料语义 + 强提示）：

1) Normalize input（`Trim + Uppercase`）  
2) Parse code → `(category_code, serial_no, suffix)`（suffix 支持 A–Z）  
3) 校验 category 一致性（解析 `category_code` 必须与 UI 当前选择分类一致）  
4) 获取 `Category.start_serial_no`（用于“强提示”，不用于强制拒绝）  
5) 按 `category_id + serial_no` 查询 group 是否存在

- IF `suffix == 'A'`：
  - IF group exists → **insert item only**
  - ELSE → **create group + insert item**
- IF `suffix != 'A'`：
  - IF group NOT exists → **VALIDATION_ERROR**（message：`"不存在 {code_with_suffix=A} 主料，替代料号不能创建"`）
  - ELSE → **insert item only**（允许断号，不要求连续）

6) （PATCHED V2）强提示规则（warning + confirm required）  
   - IF `serial_no > start_serial_no`：返回 **warning**（UI 必须二次确认；Application 允许 override，不强制拒绝）
   - warning message 规范（固定模板）：
     - `"当前输入编码已超过该分类自动起始值，确认该物料属于新编号区间"`

（PATCHED V6）Manual 模式事务边界（必须明确）

- **Manual + suffix=='A' 且 group 不存在**：
  - BEGIN
  - insert group
  - insert item(A)
  - COMMIT
- **Manual + suffix=='A' 且 group 已存在**：
  - BEGIN
  - insert item(A)（若 `UNIQUE(group_id, suffix)` 冲突则失败回滚）
  - COMMIT
- **Manual + suffix!='A'**：
  - BEGIN
  - check group exists（`category_id + serial_no`）
  - check group has anchor(A)（若无 A → 阻断写入，提示修复数据）
  - insert item
  - COMMIT
- 任一步失败：**全部回滚**；不做自动补偿（不自动补 group，不自动改号）

（PATCHED V6）warning + confirm 的可编码契约（最小实现）

- **第一次提交（未确认）**：Application 返回（不执行写入）：
  - `success=false`
  - `requires_confirmation=true`
  - `warning_code="MANUAL_CODE_ABOVE_START"`
  - `message="当前输入编码已超过该分类自动起始值，确认该物料属于新编号区间"`
- **UI 二次提交（用户确认后）**：请求附带：
  - `force_confirm=true`
- **Application 收到 `force_confirm=true`**：继续执行后续写入流程（保持同一业务校验口径）

（PATCHED V7）warning + confirm 防重复提交（必须）

- UI 在用户点击“确认”触发二次提交后：**提交按钮必须禁用**，直到请求完成（成功/失败）再恢复
- 目的：防止双击/重复点击导致重复创建
- 最小实现优先使用 UI 控制；若现有系统已具备 `request_id`/幂等机制可复用，可作为增强，但本轮不新增后端幂等设计

错误码体系约束（PATCHED，禁止扩展）：

- **不得新增错误码**（禁止新增 `CODE_DUPLICATE` 等）
- 统一使用：
  - `VALIDATION_ERROR`（字段级错误；含格式非法/分类不一致/serial_no 非法/suffix 非法/编码已存在等）
  - DB constraint violation（`UNIQUE(code)` / `UNIQUE(group_id, suffix)` / `UNIQUE(category_id, serial_no)`）→ **映射为 `VALIDATION_ERROR`**

（PATCHED V6）`VALIDATION_ERROR` 收敛（不扩展对外字段）

- 对外仍统一返回 `VALIDATION_ERROR`
- 内部日志/诊断可附带 `reason`（非正式 API 字段）：
  - `FORMAT`
  - `BUSINESS_RULE`
  - `UNIQUE_CONFLICT`
  - `DATA_INTEGRITY`

（PATCHED V2）业务错误 message 规范（冻结口径，便于解释与测试）：

- 不存在主料提示（替代料阻断）：
  - `"不存在 {code_with_suffix=A} 主料，替代料号不能创建"`
- 超过起始值强提示（warning）：
  - `"当前输入编码已超过该分类自动起始值，确认该物料属于新编号区间"`

（PATCHED V3）现有 Replacement 用例不重写（Reality Alignment）：

- **不在 V1.4 内改造替代料创建入口为 “by code”**；现有替代料创建仍以 `group_id` 为唯一入口参数（与当前 DTO/Service 对齐）。
- V1.4 新增的是“历史补录 existing_code”的入口/用例；替代料补录（suffix≠A）仅发生在该新入口内。

### 5.3 Infrastructure（SQL/Repository）

当前代码关键事实（用于对齐）：

- `SqliteMaterialRepository.GetMaxSerialNoAsync` 目前按 `material_group.category_code` 查询 max（与 V1.4 目标不一致）
- `SqliteMaterialRepository.InsertGroupAsync` 目前：
  - `INSERT material_group(category_id, category_code, serial_no) VALUES ((SELECT id FROM category WHERE code=@categoryCode), ...)`
- `InsertCategoryAsync` 当前仅插入 `code,name`

V1.4 所需最小改动：

1) `category` 表加入 `start_serial_no` 后：
   - `InsertCategoryAsync` 需写入 `start_serial_no`
   - `ListCategoriesAsync` 如 UI 需要展示/编辑该字段，则需返回该字段（新增 DTO）
2) 自动编码计算需要读取 `start_serial_no`：
   - 新增 Repository 方法：
     - `GetCategoryStartSerialNoByCodeAsync(categoryCode)` 或统一返回 Category 详情（name + start_serial_no）
3) （PATCHED）serial_no 计算与查询维度必须统一为 `category_id`
   - **所有** max serial 查询必须改为：
     - `WHERE category_id = ?`
   - `category_code` 仅用于解析与展示，不得作为业务查询维度（禁止技术债扩散）
4) （PATCHED）Manual 模式 group 复用能力（必须补齐）
   - Repository 层新增查询：
     - `GetGroupIdByCategoryIdAndSerialNo(category_id, serial_no)`（或等价方法）
   - Application 侧按该查询决定 “复用 group” 或 “新建 group”
   - 仍可复用 `InsertGroupAsync(..., serialNo)` 创建 group；但 **禁止**在 group 已存在时重复创建
5) suffix 全范围（PATCHED）
   - `material_item` 的唯一性约束仍为 `UNIQUE(group_id, suffix)`
   - manual 模式不得限制 suffix 来源（允许 A–Z 任一值，受唯一约束裁决）

（PATCHED V2）替代料合法性依赖点（Infrastructure 约束补齐）：

- group existence 判断必须基于 `category_id + serial_no`
- **禁止**使用 `category_code` 做 group existence 判断（避免语义漂移/技术债）
- group existence 是替代料（suffix≠A）合法性的唯一依据（替代料不允许触发 group 创建）

（PATCHED V5）material_group 完整性约束（禁止孤立 group）

- `material_group` 存在必须至少存在 `suffix='A'` 的 anchor item
- 若检测到 group 无 A（例如历史脏数据/迁移异常），必须视为 `VALIDATION_ERROR(type=integrity)` 并阻断对该 group 的增量写入，直到修复数据

### 5.4 Presentation（WPF / MVVM）

落点：`Presentation/ViewModels/CreateMaterialViewModel.cs` + `MainWindow.xaml`

- 新建物料页新增控件：
  - 编码方式单选：
    - 自动生成（默认）
    - 输入已有物料编码
  - 当选择“输入已有编码”时，显示 `existing_code` 输入框
- 提交逻辑：
  - 将 `code_mode` 与 `existing_code` 传给 Application
- 只读/禁用逻辑：
  - 现有字段校验/错误渲染机制复用（字段级提示）

（PATCHED V2）UI 行为补充（必须实现）：

1) 替代料输入时（suffix != A）：
   - 在提交前必须触发 group existence check（可通过调用 Application 的预校验/或直接提交后返回 `VALIDATION_ERROR` 呈现）
   - 若 group 不存在 → **直接阻断提交**，显示 message：`"不存在XXXA主料，替代料号不能创建"`
2) 超过起始值强提示（serial_no > start_serial_no）：
   - 必须弹出 confirm dialog（强提示，不可静默）
   - 用户确认后允许继续提交（override）

（PATCHED V3）UI 入口边界澄清（避免误实现）：

- `existing_code` 输入框只出现在 **新建主物料/历史补录入口**，不进入现有替代料（Replacement）页面。
- 现有替代料页面继续以 `group_id`（或页面已存在的选择锚点）发起创建，不新增“输入 ZDA0000123B”类输入框。

---

## 6. 关键流程时序（文字版）

### 6.1 自动生成编码（A）

1. 校验分类存在
2. 读取 `maxSerialNo`（该分类已存在最大 serial）
3. 读取 `start_serial_no`
4. `next = max(maxSerialNo, start_serial_no-1)+1`
5. 事务内插入 `material_group(category_id, category_code, serial_no=next)`
6. 事务内插入 `material_item(..., suffix='A', code=拼接)`
7. 并发冲突触发事务级重试（最多3次）

### 6.2 输入已有历史编码（A）

1. （PATCHED）Normalize：`existing_code = Trim + Uppercase`
2. 解析 `existing_code`（格式/拆分）→ `(category_code, serial_no, suffix)`，suffix 支持 A–Z
3. 强校验：`category_code` 必须与 UI 当前选择分类一致（否则 `VALIDATION_ERROR`）
4. 校验分类存在（category_code → category_id）
5. 校验 `serial_no >= 1`、`suffix ∈ [A..Z]`
6. （PATCHED）按 `category_id + serial_no` 查询 `material_group`：
   - IF exists：跳过 group 插入
   - ELSE：事务内插入 `material_group(serial_no=解析值)`
7. 事务内插入 `material_item(suffix=解析值, code=existing_code)`（受 `UNIQUE(code)`、`UNIQUE(group_id, suffix)` 约束）
8. DB 约束冲突 → 统一映射 `VALIDATION_ERROR`（字段级错误），禁止 silent success

### 6.3 （PATCHED V3）历史补录入口：输入已有历史编码（B–Z 替代料项）

1) Normalize：`existing_code = Trim + Uppercase`
2) 解析 → `(category_code, serial_no, suffix)`，其中 `suffix ∈ [B..Z]`
3) 校验 category 一致性（解析 `category_code` 与 UI 选择必须一致）
4) 查询 group 是否存在（`category_id + serial_no`）：
   - IF NOT exists → `VALIDATION_ERROR`（message：`"不存在 {code_with_suffix=A} 主料，替代料号不能创建"`）
   - IF exists → 仅插入 `material_item(suffix=解析值, code=existing_code)`（允许断号，无需连续）
5) 若 `serial_no > start_serial_no` → 返回 warning，UI 必须 confirm 后继续

### 6.4 （PATCHED V3）现有 Replacement 用例：基于 group_id 创建替代料（Reality Alignment）

1) UI 选择/传入 `group_id`（现有页面与 DTO 真实入口）  
2) Application 加载 group 与 **anchor item（suffix='A'）** 状态（anchor item 必须 `status=1`）  
3) suffix 分配：基于 `UNIQUE(group_id, suffix)` 与并发重试策略完成分配（不要求连续，允许 gap）  
4) 插入替代料 item（suffix 由系统内部确定）  

---

## 7. 测试清单（建议新增/调整）

### 7.1 单元测试（Application / Domain）

- `Category.start_serial_no`：
  - start=5000，max=0 → next=5000
  - start=5000，max=5008 → next=5009
- Manual 历史编码解析：
  - 合法：`ZDA0000123A` → (ZDA,123,'A')
  - 非法长度/非数字/后缀非 A-Z → validation error

### 7.2 集成测试（SQLite）

- 插入分类（带 start_serial_no）
- Auto 模式首次生成是否从 start 起
- Manual 插入 123 后，auto 仍从 start 起（不回退）
- Manual 插入 8000 后，auto=8001
- 重复 code 必须失败（`UNIQUE(code)`）
- （PATCHED）同一 `serial_no` 多 suffix：先录入 `...A` 再录入 `...B`，必须复用同一 group（不允许重复创建 group）
- （PATCHED）manual suffix 支持 A–Z：覆盖至少 `A/B/Z` 三个样例
- （PATCHED）category_id 维度：max serial 查询与 next 计算均不再依赖 `category_code`

（PATCHED V2）替代料规则与强提示测试（必须新增）：

1) 输入 `ZDA0000123B`，且 `ZDA0000123A` 不存在 → FAIL（`VALIDATION_ERROR`，message 符合模板）
2) 输入 `ZDA0000123B`，且 A 存在 → PASS
3) 输入 `ZDA0000123C`，B 不存在 → PASS（允许断号）
4) 输入 `ZDA0000501A`，start=500 → WARNING + confirm
5) 输入 `ZDA0000501B`，A 存在 → PASS
6) 输入 `ZDA0000501B`，A 不存在 → FAIL（`VALIDATION_ERROR`，message 符合模板）

（PATCHED V6）最关键回归测试（必须补齐）

- Auto 连续生成 100 次无重复（序列连续/不回退，且 code 全局唯一）
- 两线程 Auto 并发生成无重复（依赖 `UNIQUE(category_id, serial_no)` + 最多 3 次重试）
- Manual 已存在编码录入失败（`UNIQUE(code)` 冲突直接失败，不重试、不改号）
- Manual B 无 A 时失败（阻断写入，提示修复数据）
- Manual A 后再 Manual B 成功（同 group 下 suffix 唯一）
- `start_serial_no=5000` 时首次 Auto = 5000

---

## 8. 风险与回滚

### 8.1 风险点

- **迁移风险**：旧库未补 `start_serial_no` 会导致 Insert/Query 失败 → 必须先跑迁移
- **口径一致性风险**：Repository 目前 `GetMaxSerialNoAsync` 按 category_code 查询；若后续完全切到 category_id，需要同步调整 SQL 与约束映射（保持 PRD 一致）
- **错误码语义（PATCHED V4）**：历史编码重复/后缀冲突等必须以 `VALIDATION_ERROR` 字段级错误对外呈现；禁止扩展错误码体系；`CODE_CONFLICT_RETRY` 如仅为遗留实现中的内部细节，不进入 V1.4 新增逻辑与验收口径

### 8.3 （PATCHED）风险修正声明（评审必补）

- 若历史数据存在同一 `serial_no` 多 suffix，**必须复用 group**（先查 `category_id + serial_no`）。
- manual 输入的 `serial_no` **不参与“回退判断”**，但**必须参与 `max(serial_no)` 计算**（影响后续自动编码递增）。
- 所有自动序列仅依赖 `max(serial_no)` 与 `start_serial_no`，**不依赖 group 插入顺序**。

（PATCHED V2）补充风险声明（替代料语义一致性）：

- 替代料（suffix≠A）创建强依赖 group 存在；若业务误允许“无主料造组”，会破坏 group 粒度与可解释性 → 必须按本报告规则阻断。

### 8.2 回滚策略

- 新增字段可保留不影响旧逻辑（默认=1）
- UI 侧可通过隐藏入口回滚为仅自动模式（如需紧急降级，走产品发布策略；不在 V1.4 PRD 内提供运行时开关）

### 8.4 （PATCHED V6）Migration / 上线实施顺序（最小可执行）

1) 备份数据库（全量可恢复备份）  
2) 执行 `category.start_serial_no` migration（新增字段，默认值=1）  
3) 校验旧数据：`start_serial_no` 默认值均为 1（与旧行为一致）  
4) 发布应用版本（包含 V1.4 入口与规则）  
5) 开启新入口（历史补录入口 / 分类起始值维护入口）并做灰度验证  

---

## 9. 交付落点清单（按文件路径）

> 以下为建议落点（实现阶段按实际代码调整，保持最小改动）。

- **DB**
  - `category.start_serial_no` 迁移脚本（SQLite）
- **Infrastructure**
  - `SqliteMaterialRepository.InsertCategoryAsync` 增加写入 `start_serial_no`
  - 新增读取 `start_serial_no` 的查询方法
- **Application**
  - `CreateCategoryRequest/CreateCategoryResponse` 扩展 start_serial_no
  - （PATCHED V7）`CreateMaterialRequest`（或 `CreateMaterialItemRequest`）增加 `code_mode` + `existing_code`
  - `MaterialApplicationService.CreateMaterialItemA` 增加 manual 分支与 V1.4 serial 计算
- **Application（PATCHED V3）**
  - 现有 `CreateReplacement`（group_id 入口）保持不变；仅需确保其 suffix 分配口径与“gap 合法”一致（不引入 by-code 入参）
- **Presentation**
  - `CreateMaterialViewModel` 增加编码方式与输入框绑定
  - `MainWindow.xaml` 增加 UI 控件描述

---

## 10. （PATCHED V7）结构性修订输出（按评审要求）

### 10.0 标注规则（满足输出要求）

- **NEW invariants**：见文首 `（PATCHED V6）系统级最终 Invariants`
- **REFINED rules**：见 10.1（对既有规则做收敛/重定性，不扩展功能）
- **DELETED rules**：见 10.2（删除/降级冲突规则与概念）

### 10.1 变更 diff summary（按模块）

- **全局术语 / 入口模型**
  - **replacement** 入口模型强制收敛为 `group_id`（现有代码模型事实源）
  - “基准物料/anchor/base”语义统一为 **anchor item（suffix='A'）**
  - `material_group` 的稳定聚合键统一表述为：`category_id + serial_no`
- **suffix 规则**
  - （REFINED）最终口径为 **gap allowed（不要求连续）**；仅保留 `UNIQUE(group_id, suffix)` 作为约束
  - “sequence broken（不连续即报错）”相关表述不再作为运行时校验规则（见 `SUFFIX_SEQUENCE_BROKEN` 的降级说明）
- **ManualExistingCode（历史补录）**
  - 作用边界收敛为：**仅做 item 补录**；允许在 `suffix=='A'` 且 group 不存在时建组
  - 明确 `suffix!=A` 必须依赖已存在 group（禁止“无主料造组”）
- **Replacement（现有功能）**
  - 明确“不存在通过 code 创建替代料 / 通过 code 解析 group 再编排替代料”的入口
  - 明确 suffix 分配为系统内部行为（输入不包含 code/suffix）
- **错误码体系**
  - 明确 `SUFFIX_SEQUENCE_BROKEN` 不进入 V1.4 新增逻辑与验收口径
  - `CODE_CONFLICT_RETRY` 仅允许作为遗留内部细节提及，不作为 V1.4 规则输出

### 10.2 被删除/降级的规则清单

- **删除（运行时规则）**
  - suffix 连续性校验（“不连续=禁止创建”）
  - `SUFFIX_SEQUENCE_BROKEN` 作为业务错误规则（不再属于 runtime validation）
- **删除（概念/入口）**
  - “base material code（基准物料编码）”概念（系统真实入口为 `group_id`）
  - “通过编码创建替代料”的入口表达
  - “通过编码解析 group 再编排替代料”的任何表达
- **降级（仅历史概念/遗留实现细节）**
  - `SUFFIX_SEQUENCE_BROKEN`（仅允许解释“历史上曾存在”，不进入 V1.4 设计与测试口径）
  - `CODE_CONFLICT_RETRY`（不作为 V1.4 新增规则输出）

### 10.3 测试用例影响列表（文档级）

> 说明：仅列出“口径影响”，不生成/修改代码。

- **需删除或标记 obsolete**
  - `SUFFIX_SEQUENCE_BROKEN_001`（如存在于测试/契约中）
  - `SUFFIX_POLICY_AMBIGUITY_001`（若其依赖连续性=必须）
- **需统一修改为 gap allowed**
  - 所有“suffix 不连续=error”的场景 → 改为“允许创建并可补位（若实现选择补位策略）”
- **replacement 入口一致性**
  - 替代料创建相关用例：输入应仅包含 `group_id`（以及现有实现需要的 `spec/description`），不得出现“by-code / base material code”等未来式入口字段

