using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Interactivity;
using TerrariaZoomPatcher.Core;

namespace TerrariaZoomPatcher.Linux;

public sealed class MainWindow : Window
{
    private static readonly IBrush WindowBrush = Brush("#0C111C");
    private static readonly IBrush PanelBrush = Brush("#141D2D");
    private static readonly IBrush TextBrush = Brush("#EDF3FC");
    private static readonly IBrush MutedBrush = Brush("#98A8C0");
    private static readonly IBrush AccentBrush = Brush("#4F82FF");
    private static readonly IBrush SuccessBrush = Brush("#5DDCB5");
    private static readonly IBrush ErrorBrush = Brush("#FF8484");

    private readonly TextBox _pathTextBox = new();
    private readonly CheckBox _eightKCheckBox = new();
    private readonly Button _patchButton = new();
    private readonly Button _restoreButton = new();
    private readonly Label _statusLabel = new();
    private readonly TextBox _logTextBox = new();

    public MainWindow()
    {
        Title = "Terraria Ultrawide Patcher";
        Width = 800;
        Height = 720;
        MinWidth = 680;
        MinHeight = 620;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Background = WindowBrush;

        _pathTextBox.PlaceholderText =
            "/home/user/.steam/steam/steamapps/common/Terraria/Terraria.exe";
        _pathTextBox.VerticalContentAlignment = VerticalAlignment.Center;
        _pathTextBox.Background = Brush("#101827");
        _pathTextBox.Foreground = TextBrush;
        _pathTextBox.BorderBrush = Brush("#33425B");
        _pathTextBox.Padding = new Thickness(10, 8);

        _eightKCheckBox.Content =
            "Расширенные лимиты для разрешений выше 4096 px (32:9, 5120×1440, 8K)";
        _eightKCheckBox.Foreground = TextBrush;

        _statusLabel.Content = "Готово. Выберите Terraria.exe.";
        _statusLabel.Foreground = SuccessBrush;
        _statusLabel.FontWeight = FontWeight.SemiBold;
        _statusLabel.ContentTemplate = null;
        _statusLabel.HorizontalContentAlignment = HorizontalAlignment.Left;
        _statusLabel.VerticalContentAlignment = VerticalAlignment.Center;

        _logTextBox.Height = 105;
        _logTextBox.IsReadOnly = true;
        _logTextBox.AcceptsReturn = true;
        _logTextBox.TextWrapping = TextWrapping.Wrap;
        _logTextBox.Text = "Патчер работает локально и не отправляет файлы в интернет.";
        _logTextBox.Background = Brush("#141D2D");
        _logTextBox.Foreground = Brush("#C9D6EB");
        _logTextBox.BorderBrush = Brush("#33425B");
        _logTextBox.Padding = new Thickness(10);

        _patchButton.Content = "Применить патч";
        _patchButton.Background = AccentBrush;
        _patchButton.Foreground = Brushes.White;
        _patchButton.FontWeight = FontWeight.SemiBold;
        _patchButton.Padding = new Thickness(14, 10);
        _patchButton.Click += ApplyPatch_Click;

        _restoreButton.Content = "Восстановить из .bak";
        _restoreButton.Background = Brush("#202C41");
        _restoreButton.Foreground = TextBrush;
        _restoreButton.FontWeight = FontWeight.SemiBold;
        _restoreButton.Padding = new Thickness(14, 10);
        _restoreButton.Click += Restore_Click;

        Content = BuildContent();
    }

    private Control BuildContent()
    {
        var content = new StackPanel
        {
            Margin = new Thickness(28),
            Spacing = 16
        };

        var header = new StackPanel { Spacing = 5 };
        header.Children.Add(MakeText(
            "ПАТЧЕР TERRARIA ДЛЯ LINUX",
            11,
            FontWeight.Bold,
            AccentBrush));
        header.Children.Add(MakeText(
            "Ультраширокий экран без принудительного увеличения",
            23,
            FontWeight.Bold));
        header.Children.Add(MakeText(
            "Локальный графический инструмент для Terraria на экранах 21:9 и шире.",
            13,
            FontWeight.Normal,
            MutedBrush));
        content.Children.Add(header);

        var filePicker = new StackPanel { Spacing = 8 };
        filePicker.Children.Add(MakeSectionTitle("ФАЙЛ ИГРЫ"));

        var fileRow = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,Auto"),
            ColumnSpacing = 10
        };
        fileRow.Children.Add(_pathTextBox);
        var browseButton = MakeButton("Обзор…");
        browseButton.Click += Browse_Click;
        Grid.SetColumn(browseButton, 1);
        fileRow.Children.Add(browseButton);
        filePicker.Children.Add(fileRow);
        content.Children.Add(filePicker);

        content.Children.Add(MakePanel(new StackPanel
        {
            Spacing = 7,
            Children =
            {
                MakeSectionTitle("ПРОВЕРКА СОВМЕСТИМОСТИ", AccentBrush),
                MakeText(
                    "Ожидаемая структура Terraria 1.4.5.6. При несовпадении или неоднозначных сигнатурах файл останется без изменений.",
                    12,
                    FontWeight.Normal,
                    MutedBrush)
            }
        }));

        content.Children.Add(MakePanel(new StackPanel
        {
            Spacing = 7,
            Children =
            {
                _eightKCheckBox,
                MakeText(
                    "Для обычных 21:9 оставьте режим выключенным, если ширина и высота не превышают 4096 px.",
                    12,
                    FontWeight.Normal,
                    MutedBrush,
                    new Thickness(24, 0, 0, 0))
            }
        }));

        var actionRow = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,*"),
            ColumnSpacing = 10
        };
        _patchButton.HorizontalAlignment = HorizontalAlignment.Stretch;
        _restoreButton.HorizontalAlignment = HorizontalAlignment.Stretch;
        actionRow.Children.Add(_patchButton);
        Grid.SetColumn(_restoreButton, 1);
        actionRow.Children.Add(_restoreButton);
        content.Children.Add(actionRow);

        content.Children.Add(_statusLabel);
        content.Children.Add(MakeSectionTitle("ЖУРНАЛ"));
        content.Children.Add(_logTextBox);
        content.Children.Add(MakeText(
            "Перед изменением создаётся Terraria.exe.bak. Существующая резервная копия не перезаписывается.",
            11,
            FontWeight.Normal,
            MutedBrush));

        return new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Content = content
        };
    }

    private async void Browse_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            var selectedFiles = await StorageProvider.OpenFilePickerAsync(
                new FilePickerOpenOptions
                {
                    Title = "Выберите Terraria.exe",
                    AllowMultiple = false,
                    FileTypeFilter =
                    [
                        new FilePickerFileType("Terraria.exe")
                        {
                            Patterns = ["Terraria.exe"]
                        },
                        new FilePickerFileType("Все файлы")
                        {
                            Patterns = ["*"]
                        }
                    ]
                });

            if (selectedFiles.Count > 0)
            {
                _pathTextBox.Text = selectedFiles[0].Path.LocalPath;
                SetStatus("Файл выбран. Можно применить патч.", success: true);
                AppendLog($"Выбран файл: {_pathTextBox.Text}");
            }
        }
        catch (Exception exception)
        {
            SetStatus($"Не удалось открыть выбор файла: {exception.Message}", success: false);
            AppendLog(exception.Message);
        }
    }

    private async void ApplyPatch_Click(object? sender, RoutedEventArgs e)
    {
        SetBusy(true);
        try
        {
            var result = await Task.Run(() => TerrariaFilePatcher.Apply(
                _pathTextBox.Text ?? string.Empty,
                _eightKCheckBox.IsChecked == true));
            SetStatus(result.Message, result.Success);
            AppendLog(result.Message);
        }
        catch (Exception exception)
        {
            SetStatus($"Не удалось применить патч: {exception.Message}", success: false);
            AppendLog(exception.ToString());
        }
        finally
        {
            SetBusy(false);
        }
    }

    private async void Restore_Click(object? sender, RoutedEventArgs e)
    {
        string fullPath;
        try
        {
            fullPath = Path.GetFullPath((_pathTextBox.Text ?? string.Empty).Trim().Trim('"'));
        }
        catch (Exception exception)
        {
            SetStatus($"Проверьте путь к Terraria.exe: {exception.Message}", success: false);
            return;
        }

        var backupPath = TerrariaFilePatcher.GetBackupPath(fullPath);
        if (!File.Exists(backupPath))
        {
            SetStatus($"Резервная копия не найдена: {backupPath}", success: false);
            AppendLog($"Нет файла резервной копии: {backupPath}");
            return;
        }

        if (!await ConfirmRestoreAsync(backupPath))
        {
            return;
        }

        SetBusy(true);
        try
        {
            var result = await Task.Run(() => TerrariaFilePatcher.RestoreBackup(fullPath));
            SetStatus(result.Message, result.Success);
            AppendLog(result.Message);
        }
        catch (Exception exception)
        {
            SetStatus($"Не удалось восстановить файл: {exception.Message}", success: false);
            AppendLog(exception.ToString());
        }
        finally
        {
            SetBusy(false);
        }
    }

    private async Task<bool> ConfirmRestoreAsync(string backupPath)
    {
        var dialog = new Window
        {
            Title = "Подтвердите восстановление",
            Width = 470,
            SizeToContent = SizeToContent.Height,
            MinHeight = 190,
            CanResize = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Background = PanelBrush
        };
        var content = new StackPanel
        {
            Margin = new Thickness(20),
            Spacing = 14
        };
        content.Children.Add(MakeText(
            $"Текущий Terraria.exe будет заменён резервной копией:\n{backupPath}\n\nПродолжить?",
            13,
            FontWeight.Normal,
            TextBrush));

        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Spacing = 10
        };
        var cancelButton = MakeButton("Отмена");
        cancelButton.Click += (_, _) => dialog.Close(false);
        var restoreButton = MakeButton("Восстановить");
        restoreButton.Background = AccentBrush;
        restoreButton.Click += (_, _) => dialog.Close(true);
        buttons.Children.Add(cancelButton);
        buttons.Children.Add(restoreButton);
        content.Children.Add(buttons);
        dialog.Content = content;

        return await dialog.ShowDialog<bool>(this);
    }

    private void SetBusy(bool busy)
    {
        _patchButton.IsEnabled = !busy;
        _restoreButton.IsEnabled = !busy;
        _pathTextBox.IsEnabled = !busy;
        _eightKCheckBox.IsEnabled = !busy;
    }

    private void SetStatus(string message, bool success)
    {
        _statusLabel.Content = message;
        _statusLabel.Foreground = success ? SuccessBrush : ErrorBrush;
    }

    private void AppendLog(string message)
    {
        var current = _logTextBox.Text ?? string.Empty;
        _logTextBox.Text = $"{current}{Environment.NewLine}{DateTime.Now:HH:mm:ss}  {message}";
    }

    private static Border MakePanel(Control content)
    {
        return new Border
        {
            Background = PanelBrush,
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(16),
            Child = content
        };
    }

    private static TextBlock MakeSectionTitle(string text, IBrush? foreground = null)
    {
        return MakeText(
            text,
            11,
            FontWeight.Bold,
            foreground ?? MutedBrush);
    }

    private static TextBlock MakeText(
        string text,
        double fontSize,
        FontWeight fontWeight,
        IBrush? foreground = null,
        Thickness? margin = null)
    {
        return new TextBlock
        {
            Text = text,
            FontSize = fontSize,
            FontWeight = fontWeight,
            Foreground = foreground ?? TextBrush,
            TextWrapping = TextWrapping.Wrap,
            Margin = margin ?? new Thickness(0)
        };
    }

    private static Button MakeButton(string text)
    {
        return new Button
        {
            Content = text,
            Background = Brush("#202C41"),
            Foreground = TextBrush,
            Padding = new Thickness(14, 10),
            FontWeight = FontWeight.SemiBold,
            MinHeight = 42,
            Cursor = new Avalonia.Input.Cursor(Avalonia.Input.StandardCursorType.Hand)
        };
    }

    private static SolidColorBrush Brush(string color)
    {
        return new SolidColorBrush(Color.Parse(color));
    }
}
