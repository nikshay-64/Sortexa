using System;
using System.Threading;
using System.Threading.Tasks;
using Sortexa.Core.Models;

namespace Sortexa.Core.Interfaces;

public interface IExecutionEngine
{
    Task<ExecutionResult> ExecutePlanAsync(
        OrganizationPlan plan,
        IProgress<ExecutionProgress>? progress = null,
        CancellationToken cancellationToken = default);
}
