using System.Threading.Tasks;
using Sortexa.Core.Models;

namespace Sortexa.Core.Interfaces;

public interface ISettingsRepository
{
    Task<SortexaSettings> LoadSettingsAsync();
    Task SaveSettingsAsync(SortexaSettings settings);
}
