using System.Windows.Forms;
using MAPS.Desktop.Services;

namespace MAPS.Desktop.Forms.Doctor;

public partial class FrmChat : Form
{
    private readonly ApiClientService _api;

    public FrmChat(ApiClientService api)
    {
        _api = api;
        InitializeComponent();
        this.Text = "MAPS — Messages";
        this.Size = new System.Drawing.Size(900, 650);
        this.StartPosition = FormStartPosition.CenterScreen;
    }

    private void InitializeComponent()
    {
        var lblTitle = new Label
        {
            Text     = "💬 Doctor Messaging",
            Font     = new System.Drawing.Font("Segoe UI", 14, System.Drawing.FontStyle.Bold),
            Location = new System.Drawing.Point(20, 15),
            AutoSize = true
        };

        // Contacts list
        var lblContacts = new Label
        {
            Text     = "Contacts:",
            Font     = new System.Drawing.Font("Segoe UI", 9, System.Drawing.FontStyle.Bold),
            Location = new System.Drawing.Point(20, 55),
            AutoSize = true
        };

        var lstContacts = new ListBox
        {
            Name     = "lstContacts",
            Location = new System.Drawing.Point(20, 75),
            Size     = new System.Drawing.Size(220, 490),
            Font     = new System.Drawing.Font("Segoe UI", 10)
        };
        lstContacts.SelectedIndexChanged += async (s, e) => await LoadMessages();

        // Messages area
        var lblMessages = new Label
        {
            Text     = "Messages:",
            Font     = new System.Drawing.Font("Segoe UI", 9, System.Drawing.FontStyle.Bold),
            Location = new System.Drawing.Point(255, 55),
            AutoSize = true
        };

        var rtbMessages = new RichTextBox
        {
            Name      = "rtbMessages",
            Location  = new System.Drawing.Point(255, 75),
            Size      = new System.Drawing.Size(600, 430),
            ReadOnly  = true,
            BackColor = System.Drawing.Color.FromArgb(249, 250, 251),
            Font      = new System.Drawing.Font("Segoe UI", 10),
            BorderStyle = BorderStyle.FixedSingle
        };

        // Message input
        var txtInput = new TextBox
        {
            Name        = "txtInput",
            Location    = new System.Drawing.Point(255, 515),
            Size        = new System.Drawing.Size(490, 36),
            Font        = new System.Drawing.Font("Segoe UI", 11),
            PlaceholderText = "Type a message...",
            BorderStyle = BorderStyle.FixedSingle
        };
        txtInput.KeyDown += async (s, e) =>
        {
            if (e.KeyCode == Keys.Enter)
            {
                e.SuppressKeyPress = true;
                await SendMessage();
            }
        };

        var btnSend = new Button
        {
            Text      = "Send ➤",
            Location  = new System.Drawing.Point(755, 515),
            Size      = new System.Drawing.Size(100, 36),
            BackColor = System.Drawing.Color.FromArgb(59, 130, 246),
            ForeColor = System.Drawing.Color.White,
            FlatStyle = FlatStyle.Flat,
            Font      = new System.Drawing.Font("Segoe UI", 10, System.Drawing.FontStyle.Bold)
        };
        btnSend.Click += async (s, e) => await SendMessage();

        // Refresh button
        var btnRefresh = new Button
        {
            Text      = "🔄",
            Location  = new System.Drawing.Point(20, 575),
            Size      = new System.Drawing.Size(50, 30),
            FlatStyle = FlatStyle.Flat
        };
        btnRefresh.Click += async (s, e) => await LoadContacts();

        this.Controls.AddRange(new Control[]
        {
            lblTitle, lblContacts, lstContacts,
            lblMessages, rtbMessages,
            txtInput, btnSend, btnRefresh
        });
    }

    protected override async void OnLoad(EventArgs e)
    {
        base.OnLoad(e);
        await LoadContacts();
    }

    private async Task LoadContacts()
    {
        try
        {
            var result   = await _api.GetAsync<dynamic>("/api/chat/contacts");
            var lst      = (ListBox)Controls["lstContacts"]!;
            lst.Items.Clear();

            foreach (var c in result!.data)
            {
                var unread  = (int)c.unreadCount > 0 ? $" ({c.unreadCount})" : "";
                lst.Items.Add(new ContactItem
                {
                    UserId   = c.userId.ToString(),
                    Display  = $"{c.fullName}{unread}",
                    FullName = c.fullName.ToString()
                });
            }

            lst.DisplayMember = "Display";
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to load contacts: {ex.Message}", "Error",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private async Task LoadMessages()
    {
        var lst = (ListBox)Controls["lstContacts"]!;
        if (lst.SelectedItem is not ContactItem contact) return;

        try
        {
            var result = await _api.GetAsync<dynamic>(
                $"/api/chat/history/{contact.UserId}?pageSize=50");
            var rtb    = (RichTextBox)Controls["rtbMessages"]!;
            rtb.Clear();

            foreach (var msg in result!.data)
            {
                var time     = DateTime.Parse(msg.sentAt.ToString()).ToShortTimeString();
                var sender   = msg.senderName.ToString();
                var content  = msg.content.ToString();
                var isMine   = msg.senderId.ToString() !=
                               contact.UserId.ToString();

                rtb.SelectionColor = isMine
                    ? System.Drawing.Color.FromArgb(59, 130, 246)
                    : System.Drawing.Color.FromArgb(16, 185, 129);
                rtb.SelectionFont = new System.Drawing.Font(
                    "Segoe UI", 9, System.Drawing.FontStyle.Bold);
                rtb.AppendText($"\n[{time}] {sender}\n");

                rtb.SelectionColor = System.Drawing.Color.FromArgb(17, 24, 39);
                rtb.SelectionFont  = new System.Drawing.Font("Segoe UI", 10);
                rtb.AppendText($"{content}\n");
            }

            rtb.ScrollToCaret();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to load messages: {ex.Message}", "Error",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private async Task SendMessage()
    {
        var lst  = (ListBox)Controls["lstContacts"]!;
        var txt  = (TextBox)Controls["txtInput"]!;
        if (lst.SelectedItem is not ContactItem contact) return;
        if (string.IsNullOrWhiteSpace(txt.Text)) return;

        try
        {
            await _api.PostAsync($"/api/chat/contacts", new
            {
                receiverId  = contact.UserId,
                content     = txt.Text.Trim(),
                messageType = 1
            });

            txt.Clear();
            await LoadMessages();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to send message: {ex.Message}", "Error",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private class ContactItem
    {
        public string UserId   { get; set; } = string.Empty;
        public string Display  { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public override string ToString() => Display;
    }
}
