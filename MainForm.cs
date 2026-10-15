using System.Text;

namespace SPOLab2a
{
    public class MainForm : Form
    {
        private readonly TextBox code_box = new();
        private readonly Button open_button = new();
        private readonly Button calc_button = new();
        private readonly Label cl_value = new();
        private readonly Label total_value = new();
        private readonly Label rel_value = new();
        private readonly Label cli_value = new();
        private readonly ListView details = new();

        public MainForm()
        {
            Text = "СПО лаб2а метрика Джилба";
            Width = 1280;
            Height = 720;
            StartPosition = FormStartPosition.CenterScreen;
            Font = new Font("Segoe UI", 10f);

            // top panel controls
            var top = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                Height = 44,
                Padding = new Padding(6, 6, 6, 0),
                WrapContents = false
            };
            open_button.Text = "Открыть";
            open_button.AutoSize = true;
            open_button.Click += (s, e) => OpenFile();

            calc_button.Text = "Рассчитать";
            calc_button.AutoSize = true;
            calc_button.Click += (s, e) => Recalculate();

            top.Controls.AddRange([open_button, calc_button]);

            // code editor
            code_box.Multiline = true;
            code_box.AcceptsTab = true;
            code_box.AcceptsReturn = true;
            code_box.WordWrap = true;
            code_box.ScrollBars = ScrollBars.Both;
            code_box.HideSelection = false;
            code_box.Font = new Font("Consolas", 11f);
            code_box.Dock = DockStyle.Fill;

            // results panel
            var results = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                ColumnCount = 2,
                RowCount = 4,
                AutoSize = true,
                Padding = new Padding(8)
            };
            results.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 70));
            results.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 30));
            AddResultRow(results, 0, "Абсолютная сложность CL", cl_value);
            AddResultRow(results, 1, "Общее число операторов программы", total_value);
            AddResultRow(results, 2, "Относительная сложность cl = CL / операторы", rel_value);
            AddResultRow(results, 3, "Максимальный уровень вложенности CLI", cli_value);

            // additional cond operators info
            details.View = View.Details;
            details.FullRowSelect = true;
            details.GridLines = true;
            details.HideSelection = false;
            details.Dock = DockStyle.Fill;
            details.Columns.Add("Строка", 60);
            details.Columns.Add("Тип", 80);
            details.Columns.Add("Уровень", 70);
            details.Columns.Add("Оператор", 360);
            details.SelectedIndexChanged += (s, e) => HighlightSelected();

            var detailsLabel = new Label
            {
                Text = "Найденные условные операторы:",
                Dock = DockStyle.Top,
                Height = 28,
                Padding = new Padding(8, 8, 0, 0)
            };

            var rightPanel = new Panel { Dock = DockStyle.Fill };
            rightPanel.Controls.Add(details);
            rightPanel.Controls.Add(detailsLabel);
            rightPanel.Controls.Add(results);

            var split = new SplitContainer
            {
                Dock = DockStyle.Fill,
                SplitterDistance = 620
            };
            split.Panel1.Controls.Add(code_box);
            split.Panel2.Controls.Add(rightPanel);

            Controls.Add(split);
            Controls.Add(top);

            Recalculate();
        }

        private static void AddResultRow(TableLayoutPanel table, int row, string caption, Label value)
        {
            var cap = new Label
            {
                Text = caption,
                AutoSize = true,
                Anchor = AnchorStyles.Left,
                Margin = new Padding(0, 8, 0, 8)
            };
            value.AutoSize = true;
            value.Anchor = AnchorStyles.Left;
            value.Font = new Font("Segoe UI", 14f, FontStyle.Bold);
            value.Text = "—";
            table.Controls.Add(cap, 0, row);
            table.Controls.Add(value, 1, row);
        }

        private void OpenFile()
        {
            using var dlg = new OpenFileDialog
            {
                Filter = "Java (*.java)|*.java",
                Title = "Открыть файл"
            };
            if (dlg.ShowDialog(this) != DialogResult.OK) return;
            try
            {
                code_box.Text = File.ReadAllText(dlg.FileName, Encoding.UTF8);
                code_box.SelectionStart = 0;
                code_box.SelectionLength = 0;
                Recalculate();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "Не удалось открыть файл",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void Recalculate()
        {
            details.Items.Clear();
            try
            {
                GilbResult r = GilbAnalyzer.Analyze(code_box.Text);

                cl_value.Text = r.CL.ToString();
                total_value.Text = r.total_operators.ToString();
                rel_value.Text = r.Cl.ToString("0.00") + "  (" + r.CL + " / " + r.total_operators + ")";
                cli_value.Text = r.CLI().ToString();

                foreach (var c in r.conditionals)
                {
                    int shown = c.level;
                    var item = new ListViewItem(c.line.ToString());
                    item.SubItems.Add(c.kind);
                    item.SubItems.Add(shown.ToString());
                    item.SubItems.Add(c.text);
                    item.Tag = c.line;
                    details.Items.Add(item);
                }
            }
            catch (Exception ex)
            {
                cl_value.Text = total_value.Text = rel_value.Text = cli_value.Text = "ошибка";
                details.Items.Add(new ListViewItem(["", "", "", "Ошибка разбора: " + ex.Message]));
            }
        }

        private void HighlightSelected()
        {
            if (details.SelectedItems.Count == 0) return;
            if (details.SelectedItems[0].Tag is not int line) return;
            int idx = code_box.GetFirstCharIndexFromLine(line - 1);
            if (idx < 0) return;
            int lineEnd = code_box.Text.IndexOf('\n', idx);
            int length = (lineEnd < 0 ? code_box.TextLength : lineEnd) - idx;
            code_box.Select(idx, Math.Max(length, 0));
            code_box.ScrollToCaret();
        }
    }
}
