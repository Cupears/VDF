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

using Bunit;
using Microsoft.Extensions.DependencyInjection;
using VDF.Web.Services;

namespace VDF.Web.Tests.Components;

/// <summary>
/// Renders the real Settings page: the numeric-input clamps and culture-safe
/// parsing live in its markup lambdas, which compile-only verification never
/// executed.
/// </summary>
[Collection("WebSettingsOverride")]
public sealed class SettingsPageTests : BunitContext {
	readonly ScanService scan;

	public SettingsPageTests() {
		WebSettingsService.TestOverrideSettingsPath =
			Path.Combine(Path.GetTempPath(), $"VDF.WebTests.{Guid.NewGuid():N}.json");
		var webSettings = new WebSettingsService();
		scan = new ScanService(webSettings);
		Services.AddSingleton(webSettings);
		Services.AddSingleton(scan);
		JSInterop.Mode = JSRuntimeMode.Loose;
	}

	protected override void Dispose(bool disposing) {
		base.Dispose(disposing);
		scan.Dispose();
		WebSettingsService.TestOverrideSettingsPath = null;
	}

	IRenderedComponent<VDF.Web.Components.Pages.Settings> RenderPage() =>
		Render<VDF.Web.Components.Pages.Settings>();

	[Fact]
	public void DarkPhotoSwitchUpdatesTheSharedScanSettings() {
		var page = RenderPage();
		page.Find("input[aria-label='Include very dark photos']").Change(true);
		Assert.True(scan.Settings.IncludeVeryDarkPhotos);
		Assert.Contains("previously skipped as too dark", page.Markup);
	}

	[Fact]
	public void SectionLinks_StayOnSettingsDespiteTheRootBaseUrl() {
		var page = RenderPage();
		var links = page.FindAll(".settings-nav a");
		Assert.Equal(12, links.Count);
		var baseUri = new Uri("http://localhost/");
		foreach (var link in links) {
			var target = new Uri(baseUri, link.GetAttribute("href")!);
			Assert.Equal("/settings", target.AbsolutePath);
			Assert.Single(page.FindAll(target.Fragment));
		}
	}

	[Theory]
	[InlineData("-5", 1)]
	[InlineData("999", 128)]
	[InlineData("4", 4)]
	public void SlowDiskLimit_ClampsAndPersists(string typed, int expected) {
		var page = RenderPage();
		page.Find("input[aria-label='Slow disk read limit']").Change(typed);
		Assert.Equal(expected, scan.Settings.HddMaxDegreeOfParallelism);
		page.Find("button.primary").Click();
		var loaded = new VDF.Core.Settings();
		Assert.True(new WebSettingsService().Load(loaded));
		Assert.Equal(expected, loaded.HddMaxDegreeOfParallelism);
	}

	[Theory]
	[InlineData("120", 100f)] // above range
	[InlineData("30", 50f)] // below range
	[InlineData("95.5", 95.5f)] // in range, dot decimal
	[InlineData("95,5", 95.5f)] // in range, comma decimal (de-DE browser locale)
	public void AiPercentInput_ClampsAndParsesCultureSafe(string typed, float expected) {
		scan.Settings.UseAiMatching = true; // the input only renders with the feature on
		var page = RenderPage();

		page.Find("input[min='50'][max='100']").Change(typed);

		Assert.Equal(expected, scan.Settings.AiPercent);
	}

	[Theory]
	[InlineData("150", 99f)]
	[InlineData("10", 70f)]
	[InlineData("88,5", 88.5f)]
	public void AiPartialHitPercentInput_Clamps(string typed, float expected) {
		scan.Settings.EnableAiPartialDetection = true;
		var page = RenderPage();

		page.Find("input[min='70'][max='99']").Change(typed);

		Assert.Equal(expected, scan.Settings.AiPartialHitPercent);
	}

	[Fact]
	public void AiInputs_OnlyRenderWhenTheirFeatureIsEnabled() {
		scan.Settings.UseAiMatching = false;
		scan.Settings.EnableAiPartialDetection = false;
		var page = RenderPage();

		Assert.Empty(page.FindAll("input[min='50'][max='100']"));
		Assert.Empty(page.FindAll("input[min='70'][max='99']"));
	}

	[Fact]
	public void SampleCount_IsNamedForWhatItDoes() {
		// It was "Thumbnail count per file" while the results showed one thumbnail whatever
		// the count: reported as a bug by someone who had set it to 50.
		var page = RenderPage();

		var label = page.FindAll("label").Single(l => l.TextContent.Contains("Frames sampled per video"));
		label.QuerySelector("input")!.Change("7");

		Assert.Equal(7, scan.Settings.ThumbnailCount);
		Assert.DoesNotContain("Thumbnail count", page.Markup);
	}

	[Fact]
	public void ThumbnailWidthInput_ClampsToSupportedRange() {
		var webSettings = Services.GetRequiredService<WebSettingsService>();
		var page = RenderPage();

		page.Find("input[min='48'][max='960']").Change("5000");
		Assert.Equal(960, webSettings.ThumbnailWidth);

		page.Find("input[min='48'][max='960']").Change("10");
		Assert.Equal(48, webSettings.ThumbnailWidth);
	}
}
