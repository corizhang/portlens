using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using MaterialDesignThemes.Wpf;
using PortLens.Desktop.Settings;
using WpfButton = System.Windows.Controls.Button;
using WpfCheckBox = System.Windows.Controls.CheckBox;
using WpfColor = System.Windows.Media.Color;

namespace PortLens.Desktop.Dialogs;

public partial class SettingsDialog : System.Windows.Controls.UserControl
{
    private readonly List<int> _excludedPorts;
    private readonly Dictionary<string, WpfCheckBox> _frameworkToggles = new(StringComparer.OrdinalIgnoreCase);

    internal SettingsDialog(SettingsDialogState state)
    {
        InitializeComponent();
        _excludedPorts = state.ExcludedPorts.Order().ToList();

        ShowSystemPortsToggle.IsChecked = state.ShowSystemPorts;
        RememberPlacementToggle.IsChecked = state.RememberWindowPlacement;
        CloseToTrayToggle.IsChecked = state.CloseToTray;
        GroupByProjectToggle.IsChecked = state.GroupByProject;
        ShowAppMetricsToggle.IsChecked = state.ShowAppMetrics;
        SelectRefreshInterval(state.RefreshIntervalSeconds);
        BuildFrameworkRules(state.EnabledFrameworks);
        RenderBlacklist();
        SelectTab(GeneralPage, GeneralTabButton);
        UpdateBlacklistTabTitle();
    }

    private void SelectRefreshInterval(int seconds)
    {
        foreach (var item in RefreshIntervalCombo.Items.OfType<ComboBoxItem>())
        {
            if (item.Tag?.ToString() == seconds.ToString())
            {
                RefreshIntervalCombo.SelectedItem = item;
                return;
            }
        }

        RefreshIntervalCombo.SelectedIndex = 1;
    }

    private void BuildFrameworkRules(IReadOnlySet<string> enabledFrameworks)
    {
        FrameworkRulesGrid.Children.Clear();
        _frameworkToggles.Clear();
        foreach (var framework in DesktopSettings.DefaultEnabledFrameworks)
        {
            var checkBox = new WpfCheckBox
            {
                Content = framework,
                IsChecked = enabledFrameworks.Contains(framework),
                Margin = new Thickness(0, 0, 10, 8)
            };
            _frameworkToggles[framework] = checkBox;
            FrameworkRulesGrid.Children.Add(checkBox);
        }
    }

    private void RenderBlacklist()
    {
        BlacklistList.Children.Clear();
        if (_excludedPorts.Count == 0)
        {
            BlacklistList.Children.Add(new TextBlock
            {
                Text = "No hidden ports.",
                Foreground = new SolidColorBrush(WpfColor.FromRgb(124, 113, 106)),
                FontSize = 12
            });
            return;
        }

        foreach (var port in _excludedPorts.ToArray())
        {
            var row = new Grid
            {
                Margin = new Thickness(0, 0, 0, 6)
            };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            row.Children.Add(new TextBlock
            {
                Text = $":{port}",
                FontFamily = new System.Windows.Media.FontFamily("Consolas"),
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(WpfColor.FromRgb(63, 123, 200)),
                VerticalAlignment = VerticalAlignment.Center
            });

            var remove = new WpfButton
            {
                Content = "Remove",
                Style = (Style)System.Windows.Application.Current.FindResource("MaterialDesignOutlinedButton"),
                MinWidth = 76,
                Height = 28,
                Padding = new Thickness(8, 0, 8, 0)
            };
            remove.Click += (_, _) =>
            {
                _excludedPorts.Remove(port);
                RenderBlacklist();
                UpdateBlacklistTabTitle();
            };
            Grid.SetColumn(remove, 1);
            row.Children.Add(remove);
            BlacklistList.Children.Add(row);
        }
    }

    private void UpdateBlacklistTabTitle()
    {
        BlacklistTabButton.Content = $"Blacklist ({_excludedPorts.Count})";
    }

    private void GeneralTabButton_Click(object sender, RoutedEventArgs e) => SelectTab(GeneralPage, GeneralTabButton);

    private void RulesTabButton_Click(object sender, RoutedEventArgs e) => SelectTab(RulesPage, RulesTabButton);

    private void BlacklistTabButton_Click(object sender, RoutedEventArgs e) => SelectTab(BlacklistPage, BlacklistTabButton);

    private void SelectTab(FrameworkElement page, WpfButton selected)
    {
        GeneralPage.Visibility = Visibility.Collapsed;
        RulesPage.Visibility = Visibility.Collapsed;
        BlacklistPage.Visibility = Visibility.Collapsed;
        page.Visibility = Visibility.Visible;

        foreach (var tab in new[] { GeneralTabButton, RulesTabButton, BlacklistTabButton })
        {
            tab.Background = System.Windows.Media.Brushes.Transparent;
            tab.Foreground = new SolidColorBrush(WpfColor.FromRgb(103, 58, 183));
        }

        selected.Background = new SolidColorBrush(WpfColor.FromRgb(237, 230, 246));
        selected.Foreground = new SolidColorBrush(WpfColor.FromRgb(54, 30, 97));
    }

    private void ResetButton_Click(object sender, RoutedEventArgs e)
    {
        Close(new SettingsDialogResult { Reset = true });
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        var selectedSeconds = RefreshIntervalCombo.SelectedItem is ComboBoxItem item &&
                              int.TryParse(item.Tag?.ToString(), out var seconds)
            ? seconds
            : 5;

        Close(new SettingsDialogResult
        {
            ShowSystemPorts = ShowSystemPortsToggle.IsChecked == true,
            RefreshIntervalSeconds = selectedSeconds,
            RememberWindowPlacement = RememberPlacementToggle.IsChecked == true,
            CloseToTray = CloseToTrayToggle.IsChecked == true,
            GroupByProject = GroupByProjectToggle.IsChecked == true,
            ShowAppMetrics = ShowAppMetricsToggle.IsChecked == true,
            EnabledFrameworks = _frameworkToggles
                .Where(pair => pair.Value.IsChecked == true)
                .Select(pair => pair.Key)
                .ToList(),
            ExcludedPorts = _excludedPorts.ToList()
        });
    }

    private void Close(SettingsDialogResult result)
    {
        DialogHost.CloseDialogCommand.Execute(result, this);
    }
}
