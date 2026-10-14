using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Sortexa.Core.Models;

namespace Sortexa.Core.Interfaces;

public interface IFileScanner
{
    Task<IReadOnlyList<FileItem>> ScanDirectoryAsync(string directoryPath, CancellationToken cancellationToken = default);
}
