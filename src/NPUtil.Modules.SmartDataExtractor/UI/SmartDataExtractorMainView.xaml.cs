using System.Data;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using NPUtil.Modules.SmartDataExtractor.Engine;
using NPUtil.Modules.SmartDataExtractor.Export;
using NPUtil.Modules.SmartDataExtractor.Models;

namespace NPUtil.Modules.SmartDataExtractor.UI;

public partial class SmartDataExtractorMainView : UserControl
{
    private readonly SmartDataExtractorEngine _engine;
    private string? _currentFilePath;
    private SmartExtractionResult? _lastResult;

    public SmartDataExtractorMainView(SmartDataExtractorEngine engine)
    {
        InitializeComponent();
        _engine = engine;
    }

    public void SetExtractedText(string text)
    {
        TxtResultFormatted.Text = text;
        TabsResult.SelectedIndex = 1;
    }

    private void UserControl_DragOver(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            e.Effects = DragDropEffects.Copy;
            e.Handled = true;
        }
    }

    private void UserControl_Drop(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            var files = (string[])e.Data.GetData(DataFormats.FileDrop);
            if (files.Length > 0)
            {
                SetSelectedFile(files[0]);
            }
        }
    }

    private void BtnBrowseFile_Click(object sender, RoutedEventArgs e)
    {
        var ofd = new OpenFileDialog
        {
            Title = "Select File for Smart Extraction",
            Filter = "All Supported (*.pdf;*.docx;*.xlsx;*.pptx;*.csv;*.txt;*.md;*.png;*.jpg)|*.pdf;*.docx;*.xlsx;*.pptx;*.csv;*.txt;*.md;*.png;*.jpg|All Files (*.*)|*.*"
        };

        if (ofd.ShowDialog() == true)
        {
            SetSelectedFile(ofd.FileName);
        }
    }

    public void SetSelectedFile(string path)
    {
        _currentFilePath = path;
        TxtSelectedFile.Text = $"{Path.GetFileName(path)} ({new FileInfo(path).Length / 1024} KB)";
        TxtResultStatus.Text = $"Selected: {Path.GetFileName(path)}";
    }

    private async void BtnExtract_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(_currentFilePath) || !File.Exists(_currentFilePath))
        {
            MessageBox.Show("Please select or drop a valid file first.", "No File Selected", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var category = ExtractionCategory.TablesAndData;
        if (RadioFormatted.IsChecked == true) category = ExtractionCategory.FormattedText;
        else if (RadioInvoice.IsChecked == true) category = ExtractionCategory.InvoicesAndReceipts;
        else if (RadioLists.IsChecked == true) category = ExtractionCategory.ListsAndRecords;
        else if (RadioDates.IsChecked == true) category = ExtractionCategory.DatesAndDeadlines;
        else if (RadioJson.IsChecked == true) category = ExtractionCategory.StructuredJson;
        else if (RadioCustom.IsChecked == true) category = ExtractionCategory.Custom;

        var instruction = TxtCustomInstruction.Text.Trim();
        if (category == ExtractionCategory.Custom && string.IsNullOrEmpty(instruction))
        {
            instruction = "Extract structured summary and key tables";
        }

        BtnExtract.IsEnabled = false;
        TxtResultStatus.Text = "Extracting and parsing document locally...";
        TxtConfidence.Text = "Running inference...";

        var result = await _engine.ExtractAsync(_currentFilePath, category, instruction);
        _lastResult = result;
        BtnExtract.IsEnabled = true;

        if (result.Success)
        {
            TxtResultStatus.Text = result.Title;
            TxtConfidence.Text = $"Confidence: {result.Confidence:P0} • Local Processing";
            TxtElapsed.Text = $"Extraction completed in {result.ElapsedTime.TotalMilliseconds:F0} ms";

            // Bind Table View
            BindTable(result);

            // Bind Formatted Text
            TxtResultFormatted.Text = result.FormattedText;

            // Bind JSON
            TxtResultJson.Text = result.JsonOutput;

            // Bind Citations
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("Source Citations & Locality:");
            foreach (var r in result.SourceReferences)
            {
                sb.AppendLine($"• {r}");
            }

            if (result.Uncertainties.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("Quality & Uncertainty Warnings:");
                foreach (var u in result.Uncertainties)
                {
                    sb.AppendLine($"⚠ {u}");
                }
            }

            TxtCitations.Text = sb.ToString();

            // Select best tab
            if (result.TableHeaders.Count > 0 && result.TableRows.Count > 0)
            {
                TabsResult.SelectedIndex = 0;
            }
            else
            {
                TabsResult.SelectedIndex = 1;
            }
        }
        else
        {
            TxtResultStatus.Text = "Extraction failed";
            TxtConfidence.Text = string.Empty;
            MessageBox.Show($"Could not extract information: {result.ErrorMessage}", "Extraction Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void BindTable(SmartExtractionResult result)
    {
        var dt = new DataTable();
        if (result.TableHeaders.Count > 0)
        {
            foreach (var h in result.TableHeaders)
            {
                // Unique column names
                string colName = string.IsNullOrEmpty(h) ? $"Column {dt.Columns.Count + 1}" : h;
                int count = 1;
                while (dt.Columns.Contains(colName))
                {
                    colName = $"{h}_{count++}";
                }
                dt.Columns.Add(colName);
            }

            foreach (var row in result.TableRows)
            {
                var dr = dt.NewRow();
                for (int i = 0; i < Math.Min(row.Count, dt.Columns.Count); i++)
                {
                    dr[i] = row[i];
                }
                dt.Rows.Add(dr);
            }
        }

        GridResultTable.ItemsSource = dt.DefaultView;
    }

    private void BtnCopyResult_Click(object sender, RoutedEventArgs e)
    {
        if (_lastResult == null) return;

        if (TabsResult.SelectedIndex == 0 && _lastResult.TableRows.Count > 0)
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine(string.Join("\t", _lastResult.TableHeaders));
            foreach (var r in _lastResult.TableRows)
            {
                sb.AppendLine(string.Join("\t", r));
            }
            Clipboard.SetText(sb.ToString());
            TxtElapsed.Text = "Table copied to clipboard (TSV)";
        }
        else if (TabsResult.SelectedIndex == 2)
        {
            Clipboard.SetText(_lastResult.JsonOutput);
            TxtElapsed.Text = "JSON copied to clipboard";
        }
        else
        {
            Clipboard.SetText(_lastResult.FormattedText);
            TxtElapsed.Text = "Text copied to clipboard";
        }
    }

    private void BtnExport_Click(object sender, RoutedEventArgs e)
    {
        if (_lastResult == null)
        {
            MessageBox.Show("Please run an extraction before exporting.", "No Data", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var format = ((ComboBoxItem)CmbExportFormat.SelectedItem)?.Tag?.ToString() ?? "csv";
        var sfd = new SaveFileDialog
        {
            Title = $"Export Extracted Data ({format.ToUpperInvariant()})",
            FileName = $"{Path.GetFileNameWithoutExtension(_lastResult.FileName)}-extracted.{format}"
        };

        sfd.Filter = format switch
        {
            "xlsx" => "Excel Workbook (*.xlsx)|*.xlsx",
            "json" => "JSON File (*.json)|*.json",
            "txt" => "Text File (*.txt)|*.txt",
            "docx" => "Word Document (*.docx)|*.docx",
            _ => "CSV File (*.csv)|*.csv"
        };

        if (sfd.ShowDialog() == true)
        {
            try
            {
                switch (format)
                {
                    case "xlsx":
                        DataExporter.ExportToXlsx(_lastResult, sfd.FileName);
                        break;
                    case "json":
                        DataExporter.ExportToJson(_lastResult, sfd.FileName);
                        break;
                    case "txt":
                        DataExporter.ExportToTxt(_lastResult, sfd.FileName);
                        break;
                    case "docx":
                        DataExporter.ExportToDocx(_lastResult, sfd.FileName);
                        break;
                    case "csv":
                    default:
                        DataExporter.ExportToCsv(_lastResult, sfd.FileName);
                        break;
                }

                MessageBox.Show($"Exported successfully to:\n{sfd.FileName}", "Export Complete", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Export failed: {ex.Message}", "Export Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
