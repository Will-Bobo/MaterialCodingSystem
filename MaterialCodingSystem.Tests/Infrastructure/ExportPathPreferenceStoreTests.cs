using MaterialCodingSystem.Application.Interfaces;
using MaterialCodingSystem.Infrastructure.Preferences;

namespace MaterialCodingSystem.Tests.Infrastructure;

public sealed class ExportPathPreferenceStoreTests
{
    [Fact]
    public void RoundTrip_Persists_Last_Directory()
    {
        var path = Path.Combine(Path.GetTempPath(), $"mcs_prefs_{Guid.NewGuid():N}.json");
        try
        {
            IExportPathPreferenceStore store = new JsonExportPathPreferenceStore(path);
            Assert.Null(store.GetLastExportDirectory());

            var dir = @"D:\Exports\Test";
            store.SetLastExportDirectory(dir);
            var store2 = new JsonExportPathPreferenceStore(path);
            Assert.Equal(dir, store2.GetLastExportDirectory());
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }
}
