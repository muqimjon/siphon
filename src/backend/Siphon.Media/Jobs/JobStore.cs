using System.Collections.Concurrent;

namespace Siphon.Media.Jobs;

public sealed class JobStore
{
    private readonly ConcurrentDictionary<string, Job> _jobs = new();

    public bool TryAdd(Job job) => _jobs.TryAdd(job.Id, job);
    public Job? Get(string id) => _jobs.GetValueOrDefault(id);
    public bool Remove(string id) => _jobs.TryRemove(id, out _);
    public IReadOnlyCollection<Job> All => [.. _jobs.Values];

    public int ActiveFor(string callerId) =>
        _jobs.Values.Count(j => j.Caller?.Id == callerId && j.State is JobState.Queued or JobState.Running);
}
