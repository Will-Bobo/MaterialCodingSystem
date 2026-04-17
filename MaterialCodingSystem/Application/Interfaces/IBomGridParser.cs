using MaterialCodingSystem.Application.Contracts;
using MaterialCodingSystem.Domain.Services.Models;

namespace MaterialCodingSystem.Application.Interfaces;

public interface IBomGridParser
{
    Result<BomGrid> Parse(string filePath);
}

