using System;
using System.Drawing;
using System.Windows.Forms;
using System.Threading.Tasks;

namespace AccountabilityAgent
{
    public class ChatForm : Form
    {
        private readonly Func<string, Task> sendMessageAsync;
        private readonly RichTextBox chatHistory;
        private readonly TextBox inputBox;
        private readonly Button sendButton;
        private readonly CheckBox pinCheckBox;

        public ChatForm(Func<string, Task> sendMessageAsync)
        {
            this.sendMessageAsync = sendMessageAsync;

            Text = "Accountability Chat";
            Size = new Size(360, 420);
            MinimumSize = new Size(320, 360);
            StartPosition = FormStartPosition.CenterScreen;

            chatHistory = new RichTextBox
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                BackColor = Color.WhiteSmoke,
                BorderStyle = BorderStyle.None
            };

            inputBox = new TextBox
            {
                Dock = DockStyle.Fill
            };
            inputBox.KeyDown += async (sender, e) =>
            {
                if (e.KeyCode == Keys.Enter)
                {
                    e.SuppressKeyPress = true;
                    await SendCurrentMessageAsync();
                }
            };

            sendButton = new Button
            {
                Text = "Send",
                Dock = DockStyle.Right,
                Width = 70
            };
            sendButton.Click += async (sender, e) => await SendCurrentMessageAsync();

            pinCheckBox = new CheckBox
            {
                Text = "Pin",
                Dock = DockStyle.Left,
                AutoSize = true
            };
            pinCheckBox.CheckedChanged += (sender, e) =>
            {
                TopMost = pinCheckBox.Checked;
            };

            var inputPanel = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 44,
                Padding = new Padding(8)
            };

            inputPanel.Controls.Add(inputBox);
            inputPanel.Controls.Add(sendButton);
            inputPanel.Controls.Add(pinCheckBox);

            var header = new Label
            {
                Text = "Chat with parent",
                Dock = DockStyle.Top,
                Height = 32,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(8, 0, 0, 0)
            };

            Controls.Add(chatHistory);
            Controls.Add(inputPanel);
            Controls.Add(header);
        }

        public void AddMessage(string sender, string text)
        {
            if (InvokeRequired)
            {
                Invoke((MethodInvoker)(() => AddMessage(sender, text)));
                return;
            }

            var line = $"[{DateTime.Now:HH:mm}] {sender}: {text}";
            chatHistory.AppendText(line + Environment.NewLine);
            chatHistory.SelectionStart = chatHistory.TextLength;
            chatHistory.ScrollToCaret();
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            e.Cancel = true;
            Hide();
        }

        private async Task SendCurrentMessageAsync()
        {
            var text = inputBox.Text.Trim();
            if (string.IsNullOrEmpty(text)) return;
            inputBox.Text = string.Empty;
            await sendMessageAsync(text);
            AddMessage("You", text);
        }

        public void SetPinned(bool pinned)
        {
            if (InvokeRequired)
            {
                Invoke((MethodInvoker)(() => SetPinned(pinned)));
                return;
            }

            pinCheckBox.Checked = pinned;
            TopMost = pinned;
        }
    }
}
