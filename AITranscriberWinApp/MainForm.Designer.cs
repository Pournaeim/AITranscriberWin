namespace AITranscriberWinApp
{
    partial class MainForm
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.lblApiKey = new System.Windows.Forms.Label();
            this.txtApiKey = new System.Windows.Forms.TextBox();
            this.btnSaveKey = new System.Windows.Forms.Button();
            this.lblTranslationEndpoint = new System.Windows.Forms.Label();
            this.txtTranslationEndpoint = new System.Windows.Forms.TextBox();
            this.btnToggleRecording = new System.Windows.Forms.Button();
            this.btnSelectAudio = new System.Windows.Forms.Button();
            this.lblStatus = new System.Windows.Forms.Label();
            this.txtTranscript = new System.Windows.Forms.TextBox();
            this.txtTranslation = new System.Windows.Forms.TextBox();
            this.lblTranscript = new System.Windows.Forms.Label();
            this.lblTranslation = new System.Windows.Forms.Label();
            this.btnOpenOutputFolder = new System.Windows.Forms.Button();
            this.SuspendLayout();
            // 
            // lblApiKey
            // 
            this.lblApiKey.AutoSize = true;
            this.lblApiKey.Location = new System.Drawing.Point(10, 12);
            this.lblApiKey.Margin = new System.Windows.Forms.Padding(2, 0, 2, 0);
            this.lblApiKey.Name = "lblApiKey";
            this.lblApiKey.Size = new System.Drawing.Size(87, 13);
            this.lblApiKey.TabIndex = 0;
            this.lblApiKey.Text = "OpenAI API Key:";
            // 
            // txtApiKey
            // 
            this.txtApiKey.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.txtApiKey.Location = new System.Drawing.Point(112, 10);
            this.txtApiKey.Margin = new System.Windows.Forms.Padding(2, 2, 2, 2);
            this.txtApiKey.Name = "txtApiKey";
            this.txtApiKey.Size = new System.Drawing.Size(300, 20);
            this.txtApiKey.TabIndex = 1;
            this.txtApiKey.UseSystemPasswordChar = true;
            // 
            // btnSaveKey
            //
            this.btnSaveKey.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnSaveKey.Location = new System.Drawing.Point(555, 38);
            this.btnSaveKey.Name = "btnSaveKey";
            this.btnSaveKey.Size = new System.Drawing.Size(123, 27);
            this.btnSaveKey.TabIndex = 4;
            this.btnSaveKey.Text = "Save Settings";
            this.btnSaveKey.UseVisualStyleBackColor = true;
            this.btnSaveKey.Click += new System.EventHandler(this.btnSaveKey_Click);
            //
            // lblTranslationEndpoint
            //
            this.lblTranslationEndpoint.AutoSize = true;
            this.lblTranslationEndpoint.Location = new System.Drawing.Point(13, 43);
            this.lblTranslationEndpoint.Name = "lblTranslationEndpoint";
            this.lblTranslationEndpoint.Size = new System.Drawing.Size(163, 17);
            this.lblTranslationEndpoint.TabIndex = 2;
            this.lblTranslationEndpoint.Text = "Translation Service URL:";
            //
            // txtTranslationEndpoint
            //
            this.txtTranslationEndpoint.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left)
            | System.Windows.Forms.AnchorStyles.Right)));
            this.txtTranslationEndpoint.Location = new System.Drawing.Point(182, 40);
            this.txtTranslationEndpoint.Name = "txtTranslationEndpoint";
            this.txtTranslationEndpoint.Size = new System.Drawing.Size(367, 22);
            this.txtTranslationEndpoint.TabIndex = 3;
            // 
            // btnToggleRecording
            // 
            this.btnToggleRecording.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnToggleRecording.Location = new System.Drawing.Point(555, 75);
            this.btnToggleRecording.Name = "btnToggleRecording";
            this.btnToggleRecording.Size = new System.Drawing.Size(123, 32);
            this.btnToggleRecording.TabIndex = 6;
            this.btnToggleRecording.Text = "Start Recording";
            this.btnToggleRecording.UseVisualStyleBackColor = true;
            this.btnToggleRecording.Click += new System.EventHandler(this.btnToggleRecording_Click);
            // 
            // btnSelectAudio
            // 
            this.btnSelectAudio.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnSelectAudio.Location = new System.Drawing.Point(426, 75);
            this.btnSelectAudio.Name = "btnSelectAudio";
            this.btnSelectAudio.Size = new System.Drawing.Size(123, 32);
            this.btnSelectAudio.TabIndex = 5;
            this.btnSelectAudio.Text = "Transcribe File...";
            this.btnSelectAudio.UseVisualStyleBackColor = true;
            this.btnSelectAudio.Click += new System.EventHandler(this.btnSelectAudio_Click);
            // 
            // lblStatus
            // 
            this.lblStatus.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.lblStatus.Location = new System.Drawing.Point(13, 80);
            this.lblStatus.Name = "lblStatus";
            this.lblStatus.Size = new System.Drawing.Size(407, 23);
            this.lblStatus.TabIndex = 7;
            this.lblStatus.Text = "Status: Idle";
            // 
            // txtTranscript
            // 
            this.txtTranscript.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.txtTranscript.Location = new System.Drawing.Point(12, 89);
            this.txtTranscript.Margin = new System.Windows.Forms.Padding(2, 2, 2, 2);
            this.txtTranscript.Multiline = true;
            this.txtTranscript.Name = "txtTranscript";
            this.txtTranscript.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
            this.txtTranscript.Size = new System.Drawing.Size(326, 328);
            this.txtTranscript.TabIndex = 8;
            //
            // txtTranslation
            // 
            this.txtTranslation.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.txtTranslation.Location = new System.Drawing.Point(261, 89);
            this.txtTranslation.Margin = new System.Windows.Forms.Padding(2, 2, 2, 2);
            this.txtTranslation.Multiline = true;
            this.txtTranslation.Name = "txtTranslation";
            this.txtTranslation.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
            this.txtTranslation.Size = new System.Drawing.Size(330, 328);
            this.txtTranslation.TabIndex = 9;
            //
            // lblTranscript
            // 
            this.lblTranscript.AutoSize = true;
            this.lblTranscript.Location = new System.Drawing.Point(13, 110);
            this.lblTranscript.Name = "lblTranscript";
            this.lblTranscript.Size = new System.Drawing.Size(144, 17);
            this.lblTranscript.TabIndex = 10;
            this.lblTranscript.Text = "English Transcript:";
            // 
            // lblTranslation
            // 
            this.lblTranslation.AutoSize = true;
            this.lblTranslation.Location = new System.Drawing.Point(345, 110);
            this.lblTranslation.Name = "lblTranslation";
            this.lblTranslation.Size = new System.Drawing.Size(132, 17);
            this.lblTranslation.TabIndex = 11;
            this.lblTranslation.Text = "Persian Translation:";
            // 
            // btnOpenOutputFolder
            // 
            this.btnOpenOutputFolder.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.btnOpenOutputFolder.Location = new System.Drawing.Point(416, 361);
            this.btnOpenOutputFolder.Margin = new System.Windows.Forms.Padding(2, 2, 2, 2);
            this.btnOpenOutputFolder.Name = "btnOpenOutputFolder";
            this.btnOpenOutputFolder.Size = new System.Drawing.Size(123, 29);
            this.btnOpenOutputFolder.TabIndex = 12;
            this.btnOpenOutputFolder.Text = "Open Folder";
            this.btnOpenOutputFolder.UseVisualStyleBackColor = true;
            this.btnOpenOutputFolder.Click += new System.EventHandler(this.btnOpenOutputFolder_Click);
            // 
            // MainForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(690, 485);
            this.Controls.Add(this.txtTranslationEndpoint);
            this.Controls.Add(this.lblTranslationEndpoint);
            this.Controls.Add(this.btnSelectAudio);
            this.Controls.Add(this.btnOpenOutputFolder);
            this.Controls.Add(this.lblTranslation);
            this.Controls.Add(this.lblTranscript);
            this.Controls.Add(this.txtTranslation);
            this.Controls.Add(this.txtTranscript);
            this.Controls.Add(this.lblStatus);
            this.Controls.Add(this.btnToggleRecording);
            this.Controls.Add(this.btnSaveKey);
            this.Controls.Add(this.txtApiKey);
            this.Controls.Add(this.lblApiKey);
            this.Margin = new System.Windows.Forms.Padding(2, 2, 2, 2);
            this.MinimumSize = new System.Drawing.Size(535, 440);
            this.Name = "MainForm";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "AI Transcriber for Windows";
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Label lblApiKey;
        private System.Windows.Forms.TextBox txtApiKey;
        private System.Windows.Forms.Button btnSaveKey;
        private System.Windows.Forms.Button btnToggleRecording;
        private System.Windows.Forms.Button btnSelectAudio;
        private System.Windows.Forms.Label lblStatus;
        private System.Windows.Forms.TextBox txtTranscript;
        private System.Windows.Forms.TextBox txtTranslation;
        private System.Windows.Forms.Label lblTranscript;
        private System.Windows.Forms.Label lblTranslation;
        private System.Windows.Forms.Button btnOpenOutputFolder;
        private System.Windows.Forms.Label lblTranslationEndpoint;
        private System.Windows.Forms.TextBox txtTranslationEndpoint;
    }
}
