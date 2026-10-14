using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Sortexa.Core.Models;

namespace Sortexa.Core.Interfaces;

public interface IJournalRepository
{
    Task SaveJournalEntryAsync(JournalEntry entry);
    Task<IReadOnlyList<JournalEntry>> GetAllEntriesAsync();
    Task<JournalEntry?> GetEntryByIdAsync(Guid operationId);
    Task UpdateJournalEntryAsync(JournalEntry entry);
}
