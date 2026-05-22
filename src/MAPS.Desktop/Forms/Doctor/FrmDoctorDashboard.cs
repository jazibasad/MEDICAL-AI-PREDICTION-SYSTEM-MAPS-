using System.Windows.Forms;
using MAPS.Desktop.Services;

namespace MAPS.Desktop.Forms.Doctor;

public partial class FrmDoctorDashboard : Form
{
    private readonly ApiClientService _api;

    public FrmDoctorDashboard(ApiClientService api)
    {
        _api = api;
        InitializeComponent();
        this.Text = "MAPS — Doctor Dashboard";
        this.Size = new System.Drawing.Size(1200, 750);
        this.StartPosition = FormStartPosition.CenterScreen;
    }

    private void InitializeComponent()
    {
        // Title
        var lblTitle = new Label
        {
            Text     = "🩺 Doctor Dashboard — Patient Queue",
            Font     = new System.Drawing.Font("Segoe UI", 16, System.Drawing.FontStyle.Bold),
            Location = new System.Drawing.Point(20, 15),
            AutoSize = true
        };

        // Stats Panel
        var pnlStats = new FlowLayoutPanel
        {
            Location = new System.Drawing.Point(20, 55),
            Size     = new System.Drawing.Size(1140, 70),
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents  = false
        };

        foreach (var (lbl, id, color) in new[]
        {
            ("Total Patients", "lblTotal",       System.Drawing.Color.FromArgb(59,130,246)),
            ("High Risk",      "lblHighRisk",    System.Drawing.Color.FromArgb(239,68,68)),
            ("Today Preds",    "lblPredictions", System.Drawing.Color.FromArgb(16,185,129)),
            ("Unread Messages","lblMessages",    System.Drawing.Color.FromArgb(245,158,11)),
        })
        {
            var pnl = new Panel { Size = new System.Drawing.Size(260, 60), Margin = new Padding(0,0,12,0) };
            pnl.Paint += (s, e) =>
            {
                e.Graphics.FillRectangle(new System.Drawing.SolidBrush(System.Drawing.Color.FromArgb(20, color)), pnl.ClientRectangle);
                e.Graphics.DrawRectangle(new System.Drawing.Pen(color, 3), 0, 0, pnl.Width - 1, pnl.Height - 1);
            };
            var valLabel = new Label { Name = id, Text = "—", Font = new System.Drawing.Font("Segoe UI", 20, System.Drawing.FontStyle.Bold), ForeColor = color, Location = new System.Drawing.Point(12, 8), AutoSize = true };
            var lblLabel = new Label { Text = lbl, Font = new System.Drawing.Font("Segoe UI", 8), ForeColor = System.Drawing.Color.Gray, Location = new System.Drawing.Point(12, 38), AutoSize = true };
            pnl.Controls.AddRange(new Control[] { valLabel, lblLabel });
            pnlStats.Controls.Add(pnl);
        }

        // Patient Queue Grid
        var lblQueue = new Label
        {
            Text     = "Patient Queue (sorted by risk score):",
            Font     = new System.Drawing.Font("Segoe UI", 10, System.Drawing.FontStyle.Bold),
            Location = new System.Drawing.Point(20, 135),
            AutoSize = true
        };

        var dgvQueue = new DataGridView
        {
            Name       = "dgvQueue",
            Location   = new System.Drawing.Point(20, 160),
            Size       = new System.Drawing.Size(760, 530),
            ReadOnly   = true,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
            BackgroundColor = System.Drawing.Color.White,
            BorderStyle = BorderStyle.None,
            RowHeadersVisible = false,
            AllowUserToAddRows = false
        };
        dgvQueue.Columns.AddRange(
            new DataGridViewTextBoxColumn { HeaderText = "PatientId",   Name = "PatientId",   Visible = false },
            new DataGridViewTextBoxColumn { HeaderText = "Patient Name", Name = "FullName",    FillWeight = 30 },
            new DataGridViewTextBoxColumn { HeaderText = "Risk Score",   Name = "RiskScore",   FillWeight = 20 },
            new DataGridViewTextBoxColumn { HeaderText = "Urgency",      Name = "UrgencyTier", FillWeight = 20 },
            new DataGridViewTextBoxColumn { HeaderText = "Next Appt",    Name = "NextAppt",    FillWeight = 30 }
        );
        dgvQueue.CellFormatting += (s, e) =>
        {
            if (e.ColumnIndex == dgvQueue.Columns["UrgencyTier"]!.Index && e.Value != null)
            {
                e.CellStyle!.ForeColor = e.Value.ToString() switch
                {
                    "Emergency" => System.Drawing.Color.Red,
                    "Urgent"    => System.Drawing.Color.Orange,
                    "Normal"    => System.Drawing.Color.Blue,
                    _           => System.Drawing.Color.Green
                };
                e.CellStyle.Font = new System.Drawing.Font("Segoe UI", 9, System.Drawing.FontStyle.Bold);
            }
        };

        // View Patient Button
        var btnView = new Button
        {
            Text      = "👁 View Patient",
            Location  = new System.Drawing.Point(790, 160),
            Size      = new System.Drawing.Size(140, 36),
            BackColor = System.Drawing.Color.FromArgb(59, 130, 246),
            ForeColor = System.Drawing.Color.White,
            FlatStyle = FlatStyle.Flat
        };
        btnView.Click += (s, e) =>
        {
            if (dgvQueue.SelectedRows.Count == 0) return;
            MessageBox.Show($"Opening patient: {dgvQueue.SelectedRows[0].Cells["FullName"].Value}",
                "Patient Detail", MessageBoxButtons.OK, MessageBoxIcon.Information);
        };

        // Refresh Button
        var btnRefresh = new Button
        {
            Text      = "🔄 Refresh",
            Location  = new System.Drawing.Point(790, 205),
            Size      = new System.Drawing.Size(140, 36),
            FlatStyle = FlatStyle.Flat
        };
        btnRefresh.Click += async (s, e) => await LoadDashboard();

        this.Controls.AddRange(new Control[]
        {
            lblTitle, pnlStats, lblQueue, dgvQueue, btnView, btnRefresh
        });
    }

    protected override async void OnLoad(EventArgs e)
    {
        base.OnLoad(e);
        await LoadDashboard();
    }

    private async Task LoadDashboard()
    {
        try
        {
            var result = await _api.GetAsync<dynamic>("/api/doctor/dashboard");
            var d      = result!.data;

            // Update stat labels (find by name recursively)
            UpdateStat("lblTotal",       d.totalPatients.ToString());
            UpdateStat("lblHighRisk",    d.highRiskCount.ToString());
            UpdateStat("lblPredictions", d.todayPredictions.ToString());
            UpdateStat("lblMessages",    d.unreadMessages.ToString());

            var dgv = (DataGridView)Controls["dgvQueue"]!;
            dgv.Rows.Clear();

            foreach (var p in d.patientQueue)
            {
                dgv.Rows.Add(
                    p.patientId.ToString(),
                    p.fullName.ToString(),
                    $"{(double)p.riskScore:F1}",
                    p.urgencyTier.ToString(),
                    p.nextAppt != null
                        ? DateTime.Parse(p.nextAppt.ToString()).ToShortDateString()
                        : "—"
                );
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to load dashboard: {ex.Message}",
                "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void UpdateStat(string name, string value)
    {
        var ctrl = FindControl(this, name);
        if (ctrl != null) ctrl.Text = value;
    }

    private static Control? FindControl(Control parent, string name)
    {
        foreach (Control c in parent.Controls)
        {
            if (c.Name == name) return c;
            var found = FindControl(c, name);
            if (found != null) return found;
        }
        return null;
    }
}
