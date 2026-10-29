// /*
//     Copyright (C) 2026 0x90d
//     This file is part of VideoDuplicateFinder
//     VideoDuplicateFinder is free software: you can redistribute it and/or modify
//     it under the terms of the GNU Affero General Public License as published by
//     the Free Software Foundation, either version 3 of the License, or
//     (at your option) any later version.
//     VideoDuplicateFinder is distributed in the hope that it will be useful,
//     but WITHOUT ANY WARRANTY without even the implied warranty of
//     MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
//     GNU Affero General Public License for more details.
//     You should have received a copy of the GNU Affero General Public License
//     along with VideoDuplicateFinder.  If not, see <http://www.gnu.org/licenses/>.
// */
//

using System.Linq;
using System.Reactive;
using System.Threading;
using Avalonia.Collections;
using Avalonia.Input.Platform;
using ReactiveUI;
using VDF.Core.Utils;
using VDF.GUI.Data;
using VDF.GUI.Views;

namespace VDF.GUI.ViewModels {
	public sealed record ResultsSortOption(string Name, ResultsSortMode Mode);

	// Flattened results view (redesign Stage 1; the classic DataGrid view was retired
	// in Stage 6).
	public partial class MainWindowVM : ReactiveObject {

		/// <summary>Flattened list the results ListBox renders: ResultsGroupHeader + ResultsItemRow.</summary>
		public AvaloniaList<object> ResultsRows { get; } = new();

		readonly HashSet<Guid> collapsedResultsGroups = new();
		readonly HashSet<DuplicateItemVM> expandedResultsDetails = new();
		/// <summary>Groups of the last build, in display order (for navigation).</summary>
		List<ResultsGroupHeader> resultsGroups = new();
		bool resultsHavePartialClips;

		/// <summary>
		/// The Clip offset column only exists when it can carry data: partial-clip
		/// detection is enabled, or the current results (e.g. an imported scan from a
		/// machine where it was enabled) actually contain partial clips.
		/// </summary>
		public bool ResultsShowClipOffsetColumn =>
			SettingsFile.Instance.EnablePartialClipDetection || resultsHavePartialClips;

		// Seams the new results view control wires up when it attaches; the VM stays
		// ignorant of the concrete ListBox.
		internal Func<List<DuplicateItemVM>>? NewResultsSelectionProvider;
		internal Action<ResultsItemRow>? NewResultsSelectAndScrollTo;
		/// <summary>Topmost visible row + its viewport offset before a rebuild (scroll anchor, see <see cref="ResultsScrollAnchor"/>).</summary>
		internal Func<ResultsScrollAnchor.Capture?>? ResultsAnchorProvider;
		/// <summary>Scrolls the given row of the rebuilt list back to the captured viewport offset (#862).</summary>
		internal Action<object, double>? ResultsScrollToRow;
		/// <summary>The row holding keyboard focus before a rebuild, or null (see <see cref="ResultsFocusKeeper"/>).</summary>
		internal Func<object?>? ResultsFocusedRowProvider;
		/// <summary>Moves keyboard focus to the given row of the rebuilt list, unless the user put it elsewhere meanwhile.</summary>
		internal Action<object>? ResultsFocusRow;

		// ── Lazy thumbnail loading (view as you scroll) ─────────────────────────
		// The results ListBox is virtualized, so only on-screen rows are realized. Instead
		// of pre-extracting a thumbnail for every duplicate up front (expensive on slow
		// disks), the view reports the currently visible DuplicateItemVMs and we extract
		// only for those — plus a prefetch window — as they scroll into view. The disk
		// ThumbPack caches each result, so re-viewing a file is cheap.
		/// <summary>Items already queued for this results session (prevents duplicate decoding).</summary>
		readonly HashSet<DuplicateItemVM> thumbnailQueuedItems = new();
		/// <summary>Items that have had one passive decode attempt in this results session.</summary>
		readonly HashSet<DuplicateItemVM> thumbnailProcessedItems = new();
		/// <summary>Visible items awaiting their priority turn; separate from the background queue so they can be promoted.</summary>
		readonly HashSet<DuplicateItemVM> visibleThumbnailPendingItems = new();
		readonly Queue<DuplicateItemVM> visibleThumbnailQueue = new();
		readonly Queue<DuplicateItemVM> backgroundThumbnailQueue = new();
		readonly object thumbnailQueueGate = new();
		CancellationTokenSource? thumbnailQueueCts;
		Task? thumbnailQueueWorker;
		const int VisibleThumbnailBatchSize = 2;
		static readonly TimeSpan VisibleThumbnailPause = TimeSpan.FromMilliseconds(75);
		static readonly TimeSpan BackgroundThumbnailPause = TimeSpan.FromMilliseconds(650);

		/// <summary>
		/// Called by the view whenever the set of realized (viewport + prefetch) result rows
		/// changes. Visible rows are placed ahead of the passive all-results queue; scrolling
		/// never cancels active decoding or restarts work.
		/// </summary>
		internal void OnResultsVisibleItemsChanged(List<DuplicateItemVM> visibleItems) {
			if (!SettingsFile.Instance.GeneratePreviewThumbnails) return;
			EnqueuePassiveThumbnails(visibleItems, visible: true);
		}

		/// <summary>Queues every result at passive priority, while visible rows stay first.</summary>
		internal void QueueAllResultThumbnails() {
			if (!SettingsFile.Instance.GeneratePreviewThumbnails) return;
			EnqueuePassiveThumbnails(Duplicates, visible: false);
		}

		void EnqueuePassiveThumbnails(IEnumerable<DuplicateItemVM> items, bool visible) {
			bool startWorker = false;
			lock (thumbnailQueueGate) {
				var destination = visible ? visibleThumbnailQueue : backgroundThumbnailQueue;
				foreach (var item in items) {
					if (thumbnailProcessedItems.Contains(item)) continue;
					if (visible) {
						// An item can already be waiting far back in the all-results
						// queue. Add a priority entry instead of making the viewport
						// wait for every older item to finish.
						if (visibleThumbnailPendingItems.Add(item))
							destination.Enqueue(item);
					}
					else if (thumbnailQueuedItems.Add(item))
						destination.Enqueue(item);
				}
				if (thumbnailQueueWorker == null || thumbnailQueueWorker.IsCompleted) {
					thumbnailQueueCts?.Dispose();
					thumbnailQueueCts = new CancellationTokenSource();
					startWorker = true;
				}
			}
			if (startWorker) {
				thumbnailQueueWorker = ProcessPassiveThumbnailQueueAsync(thumbnailQueueCts!.Token);
			}
		}

		async Task ProcessPassiveThumbnailQueueAsync(CancellationToken token) {
			try {
				while (!token.IsCancellationRequested) {
					var batch = new List<DuplicateItemVM>(VisibleThumbnailBatchSize);
					bool hasVisibleWork;
					lock (thumbnailQueueGate) {
						while (batch.Count < VisibleThumbnailBatchSize && visibleThumbnailQueue.Count > 0) {
							var item = visibleThumbnailQueue.Dequeue();
							visibleThumbnailPendingItems.Remove(item);
							if (!thumbnailProcessedItems.Contains(item)) batch.Add(item);
						}
						hasVisibleWork = batch.Count > 0;
						// Background work is deliberately one file at a time. It must never
						// make browsing lag just because an old result set is huge.
						while (!hasVisibleWork && batch.Count < 1 && backgroundThumbnailQueue.Count > 0) {
							var item = backgroundThumbnailQueue.Dequeue();
							if (!thumbnailProcessedItems.Contains(item)) batch.Add(item);
						}
					}
					if (batch.Count == 0) return;

					// Do not call SyncCoreSettings here. It clears and repopulates the
					// scanner's include/blacklist sets; doing that from the background
					// thumbnail worker can race BuildFileList during a new scan.
					// Scan start already synchronizes these settings, and thumbnail
					// generation only reads the existing scanner configuration.
					// The saved results can include files that were moved since the
					// scan. Avoid spawning FFmpeg and flooding the UI log for each one.
					var existing = batch.Where(d => System.IO.File.Exists(d.ItemInfo.Path)).ToList();
					if (existing.Count > 0)
						await Scanner.RetrieveThumbnailsForItems(
							existing.Select(d => d.ItemInfo),
							token,
							maxDegreeOfParallelism: hasVisibleWork ? 2 : 1,
							logProgress: false);
					lock (thumbnailQueueGate)
						foreach (var item in batch)
							thumbnailProcessedItems.Add(item);
					await Task.Delay(hasVisibleWork ? VisibleThumbnailPause : BackgroundThumbnailPause, token);
				}
			}
			catch (OperationCanceledException) { }
			catch (Exception ex) {
				Logger.Instance.Error($"Passive thumbnail queue failed: {ex.Message}");
			}
		}

		/// <summary>Resets the lazy loader's memory (new scan: prior requested set is stale).</summary>
		internal void ResetLazyThumbnailState() {
			thumbnailQueueCts?.Cancel();
			lock (thumbnailQueueGate) {
				thumbnailQueuedItems.Clear();
				thumbnailProcessedItems.Clear();
				visibleThumbnailPendingItems.Clear();
				visibleThumbnailQueue.Clear();
				backgroundThumbnailQueue.Clear();
			}
		}

		public ResultsSortOption[] ResultsSortOptions { get; } = {
			new(App.Lang["Results.Sort.WastedSpace"], ResultsSortMode.WastedSpace),
			new(App.Lang["Results.Sort.TotalSize"], ResultsSortMode.TotalSize),
			new(App.Lang["Results.Sort.LargestFile"], ResultsSortMode.LargestFile),
			new(App.Lang["Results.Sort.FileCount"], ResultsSortMode.FileCount),
			new(App.Lang["Results.Sort.Similarity"], ResultsSortMode.Similarity),
			new(App.Lang["Results.Sort.DateCreated"], ResultsSortMode.DateCreated),
			new(App.Lang["Results.Sort.Duration"], ResultsSortMode.Duration),
			new(App.Lang["Results.Sort.Resolution"], ResultsSortMode.Resolution),
			new(App.Lang["Results.Sort.FolderPath"], ResultsSortMode.FolderPath),
			new(App.Lang["Results.Sort.GroupsWithChecked"], ResultsSortMode.GroupsWithCheckedItems),
		};

		public ResultsSortOption SelectedResultsSort {
			get => ResultsSortOptions.FirstOrDefault(o => o.Mode == SettingsFile.Instance.ResultsSortMode) ?? ResultsSortOptions[0];
			set {
				if (value == null || value.Mode == SettingsFile.Instance.ResultsSortMode) return;
				SettingsFile.Instance.ResultsSortMode = value.Mode;
				this.RaisePropertyChanged(nameof(SelectedResultsSort));
				RebuildResultsList();
			}
		}

		public bool ResultsSortDescending {
			get => SettingsFile.Instance.ResultsSortDescending;
			set {
				if (value == SettingsFile.Instance.ResultsSortDescending) return;
				SettingsFile.Instance.ResultsSortDescending = value;
				this.RaisePropertyChanged(nameof(ResultsSortDescending));
				RebuildResultsList();
			}
		}

		/// <summary>Shows the BEST-badged file at the top of each group, ahead of the sort order (#846).</summary>
		public bool ResultsBestFirst {
			get => SettingsFile.Instance.ResultsBestFirst;
			set {
				if (value == SettingsFile.Instance.ResultsBestFirst) return;
				SettingsFile.Instance.ResultsBestFirst = value;
				this.RaisePropertyChanged(nameof(ResultsBestFirst));
				RebuildResultsList();
			}
		}

		/// <summary>
		/// BEST badge tooltip: names the criterion that produced the winner — the ranking
		/// looked arbitrary without it (#839) — and points to where the order is configured.
		/// Null criterion = the group stayed effectively tied through every criterion.
		/// </summary>
		internal static string BestBadgeTooltip(VDF.Core.Utils.QualityRanker.Criterion<DuplicateItemVM>? decidedBy) =>
			decidedBy == null
				? App.Lang["Results.Row.BestTipTied"]
				: string.Format(App.Lang["Results.Row.BestTip"], App.Lang[$"QualityCriteria.{decidedBy.Name}"]);

		GroupSummaryFormats BuildGroupSummaryFormats() => new() {
			GroupTitle = App.Lang["Results.GroupTitle"],
			Files = App.Lang["Results.Summary.Files"],
			SingleFile = App.Lang["Results.Summary.SingleFile"],
			SaveUpTo = App.Lang["Results.Summary.SaveUpTo"],
			OnDisk = App.Lang["Results.Summary.OnDisk"],
			PreviouslyDeleted = App.Lang["Results.Summary.PreviouslyDeleted"],
		};

		static RowSpeechWords BuildRowSpeechWords() => new() {
			Best = App.Lang["Results.Row.Best"],
			AlreadyDeleted = App.Lang["DupList.AlreadyDeleted"],
			Offline = App.Lang["DupList.Offline"],
			AiMatched = App.Lang["A11y.Row.AiMatched"],
			Checked = App.Lang["Comparer.CheckedTag"],
			LanguagesLine = App.Lang["Results.Details.LanguagesLine"],
			SubtitlesLine = App.Lang["Results.Details.SubtitlesLine"],
		};

		/// <summary>Rebuilds the flattened list from the current duplicates, filter and sort.</summary>
		internal void RebuildResultsList() {
			ResultsScrollAnchor.Capture? anchor = ResultsAnchorProvider?.Invoke();
			List<Guid> oldGroupOrder = resultsGroups.ConvertAll(g => g.GroupId);
			object? focusedRow = ResultsFocusedRowProvider?.Invoke();
			int focusedIndex = focusedRow == null ? -1 : ResultsRows.IndexOf(focusedRow);
			RebuildGroupsWithOneFileLeft();
			RebuildGroupsWithAiMatch();
			ApplySizePreferenceIfChanged();
			var result = ResultsListBuilder.Build(new ResultsBuildRequest {
				Items = Duplicates.ToList(),
				Filter = DuplicatesFilterCore,
				SortMode = SettingsFile.Instance.ResultsSortMode,
				SortDescending = SettingsFile.Instance.ResultsSortDescending,
				BestFirst = SettingsFile.Instance.ResultsBestFirst,
				CollapsedGroups = collapsedResultsGroups,
				ExpandedDetails = expandedResultsDetails,
				PickBest = members => {
					var (keep, decidedBy) = VDF.Core.Utils.QualityRanker.PickKeeperWithReason(
						members.ToList(), ActiveQualityCriteria, d => d.ItemInfo.IsImage);
					return (keep, BestBadgeTooltip(decidedBy));
				},
				Formats = BuildGroupSummaryFormats(),
				SpeechWords = BuildRowSpeechWords(),
			});
			resultsGroups = result.Groups;
			resultsHavePartialClips = result.HasPartialClips;
			ResultsRows.Clear();
			ResultsRows.AddRange(result.Rows);
			this.RaisePropertyChanged(nameof(ResultsShowClipOffsetColumn));
			// The rebuild replaced every row object while the ScrollViewer kept its pixel
			// offset — without restoring the anchor, deleting groups dumps the user at a
			// random position in the list. The anchor row returns to its captured viewport
			// offset, not flush to the top, so the viewport appears to stand still (#862).
			if (anchor is { } a && ResultsScrollAnchor.FindRestoreTarget(a.Row, oldGroupOrder, result.Rows) is { } target)
				ResultsScrollToRow?.Invoke(target, a.ViewportOffsetY);
			if (ResultsFocusKeeper.FindFocusTarget(focusedRow, focusedIndex, result.Rows) is { } focusTarget)
				ResultsFocusRow?.Invoke(focusTarget);
		}

		/// <summary>Refreshes the results list after filter/sort/list changes.</summary>
		internal void RefreshResultsView() => RebuildResultsList();

		/// <summary>Builds the results list from scratch (scan done, import).</summary>
		void BuildActiveResultsView(bool resetSessionStats = true) {
			RebuildResultsList();
			QueueAllResultThumbnails();
			if (resetSessionStats)
				TotalSizeRemovedInternal = 0;
		}

		public ReactiveCommand<ResultsGroupHeader, Unit> ToggleGroupCollapsedCommand => ReactiveCommand.Create<ResultsGroupHeader>(header => {
			if (header == null) return;
			if (!collapsedResultsGroups.Remove(header.GroupId))
				collapsedResultsGroups.Add(header.GroupId);
			RebuildResultsList();
		});

		/// <summary>Expands/collapses the per-file details panel under a row (Tier 2 metadata).</summary>
		public ReactiveCommand<DuplicateItemVM, Unit> ToggleItemDetailsCommand => ReactiveCommand.Create<DuplicateItemVM>(item => {
			if (item == null) return;
			if (!expandedResultsDetails.Remove(item))
				expandedResultsDetails.Add(item);
			RebuildResultsList();
		});

		public ReactiveCommand<DuplicateItemVM, Unit> CopyItemDetailsCommand => ReactiveCommand.CreateFromTask<DuplicateItemVM>(async item => {
			if (item == null) return;
			if (ApplicationHelpers.MainWindow.Clipboard is { } clipboard)
				await clipboard.SetTextAsync(ResultsBadgeRules.BuildDetailsText(item.ItemInfo));
		});

		/// <summary>
		/// Opens the "why similar?" diagnostic for this result row vs. its group's best file,
		/// explaining which algorithm and score matched them (including cases that show 100%
		/// but look different). Reuses the single-pair report engine behind Settings → Test.
		/// </summary>
		public ReactiveCommand<DuplicateItemVM, Unit> WhySimilarCommand => ReactiveCommand.CreateFromTask<DuplicateItemVM>(async item => {
			if (item == null || IsScanning) return;
			ResultsItemRow? ownRow = resultsGroups
				.SelectMany(g => g.Rows)
				.FirstOrDefault(r => ReferenceEquals(r.Item, item));
			if (ownRow == null) return;

			// Anchor on the group's best (keep) file; only compare the best against itself as a fallback.
			var rows = ownRow.Group.Rows;
			ResultsItemRow? reference = rows.FirstOrDefault(r => r.IsBest && !ReferenceEquals(r.Item, item))
				?? rows.FirstOrDefault(r => !ReferenceEquals(r.Item, item) && !r.Item.IsOffline && !r.Item.IsTombstone)
				?? rows.FirstOrDefault(r => !ReferenceEquals(r.Item, item));
			if (reference == null) {
				await MessageBoxService.Show(App.Lang["Results.Details.WhySimilarNoPair"], title: App.Lang["Results.Details.WhySimilarTitle"]);
				return;
			}

			string fileA = item.ItemInfo.Path;
			string fileB = reference.Item.ItemInfo.Path;
			SyncCoreSettings();
			var dlg = new WhySimilarView(() => Scanner.TestFilePairAsync(fileA, fileB), fileA, fileB);
			await dlg.ShowDialog(ApplicationHelpers.MainWindow);
		});

		public ReactiveCommand<Unit, Unit> DismissResultsHintCommand => ReactiveCommand.Create(() => {
			SettingsFile.Instance.ResultsHintDismissed = true;
		});

		public ReactiveCommand<ResultsGroupHeader, Unit> CompareGroupHeaderCommand => ReactiveCommand.Create<ResultsGroupHeader>(header => {
			if (header != null) CompareGroup(header.GroupId);
		});

		/// <summary>Every metadata tag of the group's files side by side (#926), read on demand.</summary>
		public ReactiveCommand<ResultsGroupHeader, Unit> CompareMetadataOfGroupHeaderCommand => ReactiveCommand.Create<ResultsGroupHeader>(header => {
			if (header == null) return;
			var members = MetadataCompareMembers(header.GroupId);
			if (members.Count == 0) return;
			var vm = new MetadataCompareVM(members, Views.MetadataCompareWindow.LocalizedTexts());
			if (ShowMetadataCompare != null) ShowMetadataCompare(vm);
			else new Views.MetadataCompareWindow(vm).Show();
		});

		/// <summary>Test seam: receives the comparison instead of a window being opened.</summary>
		internal Action<MetadataCompareVM>? ShowMetadataCompare;

		/// <summary>The group's files in the order the results list shows them.</summary>
		internal List<DuplicateItemVM> MetadataCompareMembers(Guid groupId) {
			var shown = ResultsRows.OfType<ResultsItemRow>().Where(r => r.Item.ItemInfo.GroupId == groupId).Select(r => r.Item).ToList();
			return shown.Count > 0 ? shown : Duplicates.Where(d => d.ItemInfo.GroupId == groupId).ToList();
		}

		public ReactiveCommand<ResultsGroupHeader, Unit> KeepBestInGroupHeaderCommand => ReactiveCommand.Create<ResultsGroupHeader>(header => {
			if (header != null) KeepBestInGroup(header.GroupId);
		});

		public ReactiveCommand<ResultsGroupHeader, Unit> SelectAllInGroupHeaderCommand => ReactiveCommand.Create<ResultsGroupHeader>(header => {
			if (header != null) SelectAllInGroup(header.GroupId);
		});

		public ReactiveCommand<ResultsGroupHeader, Unit> CheckAllInGroupHeaderCommand => ReactiveCommand.Create<ResultsGroupHeader>(header => {
			if (header != null) CheckAllInGroup(header.GroupId);
		});

		public ReactiveCommand<ResultsGroupHeader, Unit> MarkGroupHeaderNotAMatchCommand => ReactiveCommand.CreateFromTask<ResultsGroupHeader>(async header => {
			if (header != null) await MarkGroupAsNotAMatch(header.GroupId);
		});

		public ReactiveCommand<ResultsGroupHeader, Unit> LoadThumbnailsForGroupHeaderCommand => ReactiveCommand.CreateFromTask<ResultsGroupHeader>(async header => {
			if (header == null) return;
			var items = Duplicates.Where(d => d.ItemInfo.GroupId == header.GroupId).Select(d => d.ItemInfo).ToList();
			if (items.Count == 0) return;
			SyncCoreSettings();
			await Scanner.RetrieveThumbnailsForItems(items);
		});

		public ReactiveCommand<Unit, Unit> ToggleResultsDensityCommand => ReactiveCommand.Create(() => {
			SettingsFile.Instance.ResultsCompactRows = !SettingsFile.Instance.ResultsCompactRows;
		});

		/// <summary>Group navigation for the flattened view.</summary>
		Guid? NavigateGroupNewView(bool forward, Guid? fromGroupId = null) {
			if (resultsGroups.Count == 0) return null;

			Guid? referenceGroupId = fromGroupId ?? GetSelectedDuplicateItem()?.ItemInfo.GroupId;
			int currentIndex = -1;
			if (referenceGroupId.HasValue)
				currentIndex = resultsGroups.FindIndex(g => g.GroupId == referenceGroupId.Value);

			int targetIndex = forward
				? (currentIndex + 1 < resultsGroups.Count ? currentIndex + 1 : 0)
				: (currentIndex - 1 >= 0 ? currentIndex - 1 : resultsGroups.Count - 1);

			var target = resultsGroups[targetIndex];
			if (target.IsCollapsed) {
				collapsedResultsGroups.Remove(target.GroupId);
				RebuildResultsList();
				target = resultsGroups.FirstOrDefault(g => g.GroupId == target.GroupId) ?? target;
			}
			var firstRow = target.Rows.FirstOrDefault();
			if (firstRow == null) return null;
			NewResultsSelectAndScrollTo?.Invoke(firstRow);
			return target.GroupId;
		}
	}
}
