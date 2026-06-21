using DStockAnalysis.Models;
using DStockAnalysis.ViewModels;
using Xunit;

namespace DStockAnalysis.Tests.Integration;

/// <summary>
/// MainViewModel を起点とした画面横断フローの結合テスト。
/// 既定では %AppData% を参照するが、本テストは件数固定値に依存せず動作するよう記述している。
/// </summary>
public class MainViewModelIntegrationTests
{
    private static MainViewModel NewVm() => new();

    [Fact] // IT-MV-01: 起動時に銘柄が読み込まれ、初期画面はスクリーニング
    public void Startup_LoadsStocks_DefaultsToScreening()
    {
        var vm = NewVm();
        Assert.True(vm.AllStocks.Count > 0);
        Assert.Same(vm.ScreeningVM, vm.CurrentViewModel);
        Assert.Equal("Screening", vm.ActiveScreen);
        Assert.Equal(vm.AllStocks.Count, vm.ScreeningVM.FilteredStocks.Count);
    }

    [Fact] // IT-MV-02: 画面切替コマンド
    public void Navigation_SwitchesCurrentViewModel()
    {
        var vm = NewVm();
        vm.ShowAnalysisCommand.Execute(null);
        Assert.Same(vm.AnalysisVM, vm.CurrentViewModel);
        vm.ShowComparisonCommand.Execute(null);
        Assert.Same(vm.ComparisonVM, vm.CurrentViewModel);
        vm.ShowScreeningCommand.Execute(null);
        Assert.Same(vm.ScreeningVM, vm.CurrentViewModel);
    }

    [Fact] // IT-MV-03: 市場トグルで一覧が絞り込まれる
    public void Screening_MarketToggle_Filters()
    {
        var vm = NewVm();
        vm.ScreeningVM.MarketPR = true;
        Assert.All(vm.ScreeningVM.FilteredStocks,
            s => Assert.Contains("プライム", s.Market));
        // 全件以下になっている(プライム以外が除外される)
        Assert.True(vm.ScreeningVM.FilteredStocks.Count <= vm.AllStocks.Count);
    }

    [Fact] // IT-MV-04: プリセット適用で条件が反映される
    public void Screening_ApplyPreset_FiltersByCriteria()
    {
        var vm = NewVm();
        var preset = vm.ScreeningVM.Presets.First(p => p.Name == "株主優待あり高利回り銘柄");
        vm.ScreeningVM.ApplyPresetCommand.Execute(preset);
        Assert.All(vm.ScreeningVM.FilteredStocks, s => Assert.True(s.HasShareholderBenefit));
    }

    [Fact] // IT-MV-05: スクリーニング→個別分析へ遷移し、詳細(チャート・レーダー・チェック)が構築される
    public void OpenAnalysis_BuildsDetail()
    {
        var vm = NewVm();
        var target = vm.AllStocks.First(s => s.History.Count > 0);
        vm.OpenAnalysis(target);

        Assert.Same(vm.AnalysisVM, vm.CurrentViewModel);
        Assert.Same(target, vm.AnalysisVM.SelectedStock);
        Assert.True(vm.AnalysisVM.Charts.Count > 0);
        Assert.Equal(6, vm.AnalysisVM.Radar.Count);
        Assert.Equal(15, vm.AnalysisVM.BuffettItems.Count);
    }

    [Fact] // IT-MV-06: バフェットチェック変更でスコアが再計算される
    public void AnalysisCheckChange_RecalculatesScore()
    {
        var vm = NewVm();
        var target = vm.AllStocks.First();
        vm.OpenAnalysis(target);

        // 全項目「いいえ」→ スコア記録 → 全項目「はい」へ
        foreach (var item in vm.AnalysisVM.BuffettItems) item.Answer = YesNoUnknown.No;
        var low = target.BuffettScore;
        foreach (var item in vm.AnalysisVM.BuffettItems) item.Answer = YesNoUnknown.Yes;
        var high = target.BuffettScore;

        Assert.True(high > low, $"high={high} low={low}");
        Assert.InRange(target.BuffettScore, 0, 100);
    }

    [Fact] // IT-MV-07: 比較に追加すると比較対象とグラフが構築される
    public void Comparison_Add_BuildsRowsAndCharts()
    {
        var vm = NewVm();
        var a = vm.AllStocks[0];
        var b = vm.AllStocks[1];
        vm.ComparisonVM.Add(a);
        vm.ComparisonVM.Add(b);

        Assert.Contains(a, vm.ComparisonVM.Selected);
        Assert.Contains(b, vm.ComparisonVM.Selected);
        Assert.True(vm.ComparisonVM.Charts.Count > 0);

        // 同一銘柄の重複追加は無視
        var beforeCount = vm.ComparisonVM.Selected.Count;
        vm.ComparisonVM.Add(a);
        Assert.Equal(beforeCount, vm.ComparisonVM.Selected.Count);
    }

    [Fact] // IT-MV-08: 比較対象の削除
    public void Comparison_Remove_Works()
    {
        var vm = NewVm();
        var a = vm.AllStocks[0];
        vm.ComparisonVM.Add(a);
        Assert.Contains(a, vm.ComparisonVM.Selected);
        vm.ComparisonVM.Remove(a);
        Assert.DoesNotContain(a, vm.ComparisonVM.Selected);
    }
}
