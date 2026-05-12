using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Media;
using MaDB.Desktop.ViewModels;

namespace MaDB.Desktop.Views;

internal static class QueryResultGridColumns
{
    public static void Rebuild(DataGrid dataGrid, IReadOnlyList<string> columnNames, bool isReadOnly = true)
    {
        dataGrid.Columns.Clear();

        foreach (var columnName in columnNames)
        {
            if (isReadOnly)
            {
                dataGrid.Columns.Add(new DataGridTemplateColumn
                {
                    Header = columnName,
                    MinWidth = 120,
                    Width = DataGridLength.SizeToCells,
                    CellTemplate = new FuncDataTemplate<QueryResultGridRow>((row, _) => new TextBlock
                    {
                        Margin = new Thickness(8, 0),
                        VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
                        Text = row.GetValue(columnName),
                        TextTrimming = TextTrimming.CharacterEllipsis
                    })
                });
            }
            else
            {
                dataGrid.Columns.Add(new DataGridTemplateColumn
                {
                    Header = columnName,
                    MinWidth = 120,
                    Width = DataGridLength.SizeToCells,
                    CellTemplate = new FuncDataTemplate<QueryResultGridRow>((row, _) => new TextBlock
                    {
                        Margin = new Thickness(8, 0),
                        VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
                        Text = row.GetValue(columnName),
                        TextTrimming = TextTrimming.CharacterEllipsis
                    }),
                    CellEditingTemplate = new FuncDataTemplate<QueryResultGridRow>((row, _) => new TextBox
                    {
                        Margin = new Thickness(2),
                        Text = row.GetValue(columnName),
                        [!TextBox.TextProperty] = new Avalonia.Data.Binding
                        {
                            Path = $"[{columnName}]",
                            Mode = Avalonia.Data.BindingMode.TwoWay
                        }
                    })
                });
            }
        }
    }
}
