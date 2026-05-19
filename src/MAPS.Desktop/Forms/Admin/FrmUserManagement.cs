using System.Windows.Forms;
using MAPS.Desktop.Services;

namespace MAPS.Desktop.Forms.Admin;

public partial class FrmUserManagement : Form
{
    private readonly ApiClientService _api;
    private int _currentPage = 1;

    public FrmUserManagement(ApiClientService api)
    {
        _api = api;
        InitializeComponent();
        this.Text = "MAPS — User Management";
        this.Size = new System.Drawing.Size(1100, 700);
    }

    private void InitializeComponent()
    {
        // Title Label
        var lblTitle = new Label
        {
            Text = "User Management",
            Font = new System.Drawing.Font("Segoe UI", 16, System.Drawing.FontStyle.Bold),
            Location = new System.Drawing.Point(20, 15),
            AutoSize = true
        };

        // Search Box
        var lblSearch = new Label { Text = "Search:", Location = new System.Drawing.Point(20, 60), AutoSize = true };
        var txtSearch = new TextBox
        {
            Location = new System.Drawing.Point(70, 57),
            Width = 220,
            PlaceholderText = "Name or email..."
        };
        txtSearch.TextChanged += (s, e) => LoadUsers(txtSearch.Text);

        // Approve Button
        var btnApprove = new Button
        {
            Text = "✓ Approve Selected",
            Location = new System.Drawing.Point(750, 55),
            Width = 150,
            Height = 32,
            BackColor = System.Drawing.Color.FromArgb(16, 185, 129),
            ForeColor = System.Drawing.Color.White,
            FlatStyle = FlatStyle.Flat
        };
        btnApprove.Click += async (s, e) => await ApproveSelected();

        // Deactivate Button
        var btnDeactivate = new Button
        {
            Text = "⊘ Deactivate",
            Location = new System.Drawing.Point(910, 55),
            Width = 130,
            Height = 32,
            BackColor = System.Drawing.Color.FromArgb(245, 158, 11),
            ForeColor = System.Drawing.Color.White,
            FlatStyle = FlatStyle.Flat
        };
        btnDeactivate.Click += async (s, e) => await DeactivateSelected();

        // Users DataGridView
        var dgvUsers = new DataGridView
        {
            Name     = "dgvUsers",
            Location = new System.Drawing.Point(20, 100),
            Size     = new System.Drawing.Size(1040, 520),
            ReadOnly = true,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
            BackgroundColor = System.Drawing.Color.White,
            BorderStyle = BorderStyle.None,
            RowHeadersVisible = false,
            AllowUserToAddRows = false
        };
        dgvUsers.Columns.AddRange(
            new DataGridViewTextBoxColumn { HeaderText = "ID",         Name = "UserId",    Visible = false },
            new DataGridViewTextBoxColumn { HeaderText = "Full Name",  Name = "FullName",  FillWeight = 25 },
            new DataGridViewTextBoxColumn { HeaderText = "Email",      Name = "Email",     FillWeight = 30 },
            new DataGridViewTextBoxColumn { HeaderText = "Role",       Name = "Role",      FillWeight = 10 },
            new DataGridViewTextBoxColumn { HeaderText = "Active",     Name = "IsActive",  FillWeight = 10 },
            new DataGridViewTextBoxColumn { HeaderText = "Approved",   Name = "IsApproved",FillWeight = 10 },
            new DataGridViewTextBoxColumn { HeaderText = "Registered", Name = "CreatedAt", FillWeight = 15 }
        );

        this.Controls.AddRange(new Control[]
        {
            lblTitle, lblSearch, txtSearch,
            btnApprove, btnDeactivate, dgvUsers
        });
    }

    protected override async void OnLoad(EventArgs e)
    {
        base.OnLoad(e);
        await LoadUsers();
    }

    private async Task LoadUsers(string search = "")
    {
        try
        {
            var result = await _api.GetAsync<dynamic>($"/api/users?searchTerm={search}&page={_currentPage}");
            var dgv    = (DataGridView)Controls["dgvUsers"]!;
            dgv.Rows.Clear();

            foreach (var user in result.data.items)
            {
                dgv.Rows.Add(
                    user.userId.ToString(),
                    user.fullName.ToString(),
                    user.email.ToString(),
                    user.role.ToString(),
                    (bool)user.isActive   ? "✓" : "✗",
                    (bool)user.isApproved ? "✓" : "Pending",
                    DateTime.Parse(user.createdAt.ToString()).ToShortDateString()
                );
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to load users: {ex.Message}", "Error",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private async Task ApproveSelected()
    {
        var dgv = (DataGridView)Controls["dgvUsers"]!;
        if (dgv.SelectedRows.Count == 0) return;

        var userId = dgv.SelectedRows[0].Cells["UserId"].Value?.ToString();
        if (userId is null) return;

        var confirm = MessageBox.Show("Approve this user?", "Confirm",
            MessageBoxButtons.YesNo, MessageBoxIcon.Question);
        if (confirm != DialogResult.Yes) return;

        await _api.PostAsync($"/api/users/{userId}/approve", null);
        await LoadUsers();
    }

    private async Task DeactivateSelected()
    {
        var dgv = (DataGridView)Controls["dgvUsers"]!;
        if (dgv.SelectedRows.Count == 0) return;

        var userId = dgv.SelectedRows[0].Cells["UserId"].Value?.ToString();
        if (userId is null) return;

        var confirm = MessageBox.Show("Deactivate this user?", "Confirm",
            MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
        if (confirm != DialogResult.Yes) return;

        await _api.PutAsync($"/api/users/{userId}/deactivate", null);
        await LoadUsers();
    }
}
