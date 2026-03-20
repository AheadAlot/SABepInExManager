using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using FluentIcons.Avalonia;
using FluentIcons.Common;

namespace SABepInExManager.Services;

public sealed class DialogService
{
    public async Task<bool> ShowConfirmAsync(Window owner, ConfirmDialogOptions options)
    {
        var tcs = new TaskCompletionSource<bool>();

        var iconBlock = new SymbolIcon
        {
            Symbol = Symbol.Warning,
            FontSize = 20,
            VerticalAlignment = VerticalAlignment.Center,
            Classes = { "dialog-danger-icon" },
            IsVisible = options.IsDangerous,
        };

        var titleBlock = new TextBlock
        {
            Text = options.Title,
            Classes = { "dialog-title" },
            TextWrapping = TextWrapping.Wrap,
        };

        var header = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("Auto,*"),
            ColumnSpacing = 8,
            Children =
            {
                iconBlock,
                titleBlock,
            }
        };
        Grid.SetColumn(titleBlock, 1);

        var messageBlock = new TextBlock
        {
            Text = options.Message,
            TextWrapping = TextWrapping.Wrap,
        };

        var messagePanel = new StackPanel
        {
            Spacing = 6,
            Children =
            {
                messageBlock,
            }
        };

        if (!string.IsNullOrWhiteSpace(options.WarningHint))
        {
            messagePanel.Children.Add(new TextBlock
            {
                Text = options.WarningHint,
                Classes = { "dialog-caption" },
            });
        }

        var messageScrollViewer = new ScrollViewer
        {
            VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,
            Content = messagePanel,
        };

        var cancelButton = new Button
        {
            Content = options.CancelText,
            MinWidth = 96,
            IsCancel = true,
            Classes = { "dialog-secondary-action" },
        };

        var confirmButton = new Button
        {
            Content = options.ConfirmText,
            MinWidth = 96,
            IsDefault = true,
            Classes = { options.IsDangerous ? "dialog-danger-action" : "dialog-primary-action" },
        };

        var actionsPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            HorizontalAlignment = HorizontalAlignment.Right,
            Children =
            {
                cancelButton,
                confirmButton,
            }
        };

        var content = new Grid
        {
            RowDefinitions = new RowDefinitions("Auto,*,Auto"),
            RowSpacing = 12,
            Children =
            {
                header,
                messageScrollViewer,
                actionsPanel,
            }
        };
        Grid.SetRow(messageScrollViewer, 1);
        Grid.SetRow(actionsPanel, 2);

        var dialog = new Window
        {
            Title = "警告",
            Width = 520,
            MinWidth = 480,
            MaxWidth = 640,
            MinHeight = 220,
            Height = 260,
            SizeToContent = SizeToContent.Manual,
            Classes = { "app-dialog" },
            Content = new Border
            {
                Classes = { "app-dialog-container" },
                Child = content,
            }
        };

        confirmButton.Click += (_, _) =>
        {
            tcs.TrySetResult(true);
            dialog.Close();
        };

        cancelButton.Click += (_, _) =>
        {
            tcs.TrySetResult(false);
            dialog.Close();
        };

        dialog.Closed += (_, _) => tcs.TrySetResult(false);

        _ = dialog.ShowDialog(owner);
        return await tcs.Task;
    }

    public async Task ShowHelpAsync(Window owner, string title, string message)
    {
        var closeButton = new Button
        {
            Content = "关闭",
            HorizontalAlignment = HorizontalAlignment.Right,
            MinWidth = 96,
            IsDefault = true,
            IsCancel = true,
            Classes = { "dialog-secondary-action" },
        };

        var content = new Grid
        {
            RowDefinitions = new RowDefinitions("Auto,*,Auto"),
            RowSpacing = 12,
            Children =
            {
                new TextBlock
                {
                    Text = title,
                    Classes = { "dialog-title" },
                },
                new Border
                {
                    Classes = { "dialog-message-box", "default-tone" },
                    Child = new ScrollViewer
                    {
                        VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
                        MaxHeight = 420,
                        Content = new TextBlock
                        {
                            Text = message,
                            TextWrapping = TextWrapping.Wrap,
                        }
                    }
                },
                closeButton,
            }
        };
        Grid.SetRow(content.Children[1], 1);
        Grid.SetRow(closeButton, 2);

        var dialog = new Window
        {
            Title = title,
            Width = 680,
            MinWidth = 560,
            MinHeight = 360,
            SizeToContent = SizeToContent.Manual,
            Classes = { "app-dialog" },
            Content = new Border
            {
                Classes = { "app-dialog-container" },
                Child = content,
            }
        };

        closeButton.Click += (_, _) => dialog.Close();
        await dialog.ShowDialog(owner);
    }
}

public sealed class ConfirmDialogOptions
{
    public required string Title { get; init; }
    public required string Message { get; init; }
    public required string ConfirmText { get; init; }
    public string CancelText { get; init; } = "取消";
    public bool IsDangerous { get; init; }
    public string? WarningHint { get; init; }
}

