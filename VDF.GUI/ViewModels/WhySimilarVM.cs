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

using System.Reactive;
using System.Threading.Tasks;
using Avalonia.Input.Platform;
using ReactiveUI;
using VDF.GUI.Views;

namespace VDF.GUI.ViewModels {

	/// <summary>
	/// "Why similar?" dialog. Runs the single-pair detection diagnostic (<see cref="ScanEngine.TestFilePairAsync"/>)
	/// for a result row vs. its group's best file and shows the report — so a result that says 100%
	/// but looks different can be explained (which algorithm, what score, what threshold).
	/// </summary>
	public sealed class WhySimilarVM : ReactiveObject {
		readonly WhySimilarView? host;
		readonly Func<Task<string>> reportProducer;

		public WhySimilarVM(WhySimilarView host, Func<Task<string>> reportProducer, string fileA, string fileB) {
			this.host = host;
			this.reportProducer = reportProducer;
			Intro = App.Lang["Results.Details.WhySimilarIntro"] + "\n" + fileA + "\n" + fileB;
			_ = LoadAsync();
		}

		public string Intro { get; }

		string _Report = string.Empty;
		public string Report {
			get => _Report;
			set => this.RaiseAndSetIfChanged(ref _Report, value);
		}

		bool _IsWorking = true;
		public bool IsWorking {
			get => _IsWorking;
			set => this.RaiseAndSetIfChanged(ref _IsWorking, value);
		}

		public string WorkingText => App.Lang["Results.Details.WhySimilarWorking"];

		async Task LoadAsync() {
			IsWorking = true;
			try {
				Report = await reportProducer();
			}
			finally {
				IsWorking = false;
			}
		}

		public ReactiveCommand<Unit, Unit> CopyCommand => ReactiveCommand.Create(() => {
			if (!string.IsNullOrEmpty(Report))
				ApplicationHelpers.MainWindow.Clipboard?.SetTextAsync(Report);
		});

		public ReactiveCommand<Unit, Unit> CloseCommand => ReactiveCommand.Create(() => host?.Close());
	}
}
