using System;
using System.Threading;
using System.Threading.Tasks;
using Sortexa.Core.Models;

namespace Sortexa.Core.Interfaces;

public interface IUndoEngine
{
    Task<UndoResult> UndoOperationAsync(
        JournalEntry journalEntry,
        IProgress<ExecutionProgress>? progress = null,
        CancellationToken cancellationToken = default);
}
