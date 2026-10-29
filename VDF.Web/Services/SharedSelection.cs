using System.Collections;
using System.Collections.Concurrent;
using VDF.Core.ViewModels;

namespace VDF.Web.Services;

/// <summary>One thread-safe checked-file set for every browser attached to this server.</summary>
public sealed class SharedSelection(Action changed) : IReadOnlySet<DuplicateItem> {
    readonly ConcurrentDictionary<DuplicateItem, byte> items = new();
    public int Count => items.Count;
    public bool Contains(DuplicateItem item) => items.ContainsKey(item);
    public bool Add(DuplicateItem item) { if (!items.TryAdd(item, 0)) return false; changed(); return true; }
    public bool Remove(DuplicateItem item) { if (!items.TryRemove(item, out _)) return false; changed(); return true; }
    public void Clear() { if (items.IsEmpty) return; items.Clear(); changed(); }
    HashSet<DuplicateItem> Snapshot() => new(items.Keys);
    public IEnumerator<DuplicateItem> GetEnumerator() => items.Keys.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    public bool IsProperSubsetOf(IEnumerable<DuplicateItem> other) => Snapshot().IsProperSubsetOf(other);
    public bool IsProperSupersetOf(IEnumerable<DuplicateItem> other) => Snapshot().IsProperSupersetOf(other);
    public bool IsSubsetOf(IEnumerable<DuplicateItem> other) => Snapshot().IsSubsetOf(other);
    public bool IsSupersetOf(IEnumerable<DuplicateItem> other) => Snapshot().IsSupersetOf(other);
    public bool Overlaps(IEnumerable<DuplicateItem> other) => Snapshot().Overlaps(other);
    public bool SetEquals(IEnumerable<DuplicateItem> other) => Snapshot().SetEquals(other);
}
