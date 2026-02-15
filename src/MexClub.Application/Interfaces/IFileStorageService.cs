namespace MexClub.Application.Interfaces;

public interface IFileStorageService
{
    Task<string> SaveFileAsync(byte[] fileBytes, string fileName, string subfolder, CancellationToken ct = default);
    Task<bool> DeleteFileAsync(string filePath, CancellationToken ct = default);
    Task<byte[]?> GetFileAsync(string filePath, CancellationToken ct = default);
    string GetFileUrl(string relativePath);
}
