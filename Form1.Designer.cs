namespace winProyComunicacion
{
    partial class Form1
    {
        /// <summary>
        ///  Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        ///  Clean up any resources being used.
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
        ///  Required method for Designer support - do not modify
        ///  the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            txtMensaje = new TextBox();
            btnEnviaMensaje = new Button();
            rchConversacion = new RichTextBox();
            cmbVelocidad = new ComboBox();
            cmbPuerto = new ComboBox();
            btnConectar = new Button();
            btnEnviarArchivo = new Button();
            prgArchivo = new ProgressBar();
            label1 = new Label();
            SuspendLayout();
            // 
            // txtMensaje
            // 
            txtMensaje.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Right;
            txtMensaje.Location = new Point(246, 49);
            txtMensaje.Margin = new Padding(3, 4, 3, 4);
            txtMensaje.Multiline = true;
            txtMensaje.Name = "txtMensaje";
            txtMensaje.Size = new Size(257, 173);
            txtMensaje.TabIndex = 0;
            txtMensaje.KeyDown += txtMensaje_KeyDown;
            // 
            // btnEnviaMensaje
            // 
            btnEnviaMensaje.Anchor = AnchorStyles.Right;
            btnEnviaMensaje.Location = new Point(517, 55);
            btnEnviaMensaje.Margin = new Padding(3, 4, 3, 4);
            btnEnviaMensaje.Name = "btnEnviaMensaje";
            btnEnviaMensaje.Size = new Size(74, 32);
            btnEnviaMensaje.TabIndex = 1;
            btnEnviaMensaje.Text = "Enviar";
            btnEnviaMensaje.UseVisualStyleBackColor = true;
            btnEnviaMensaje.Click += btnEnviaMensaje_Click;
            // 
            // rchConversacion
            // 
            rchConversacion.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            rchConversacion.Location = new Point(14, 37);
            rchConversacion.Margin = new Padding(3, 4, 3, 4);
            rchConversacion.Name = "rchConversacion";
            rchConversacion.ReadOnly = true;
            rchConversacion.Size = new Size(207, 377);
            rchConversacion.TabIndex = 2;
            rchConversacion.Text = "";
            // 
            // cmbVelocidad
            // 
            cmbVelocidad.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            cmbVelocidad.FormattingEnabled = true;
            cmbVelocidad.Location = new Point(447, 244);
            cmbVelocidad.Margin = new Padding(3, 4, 3, 4);
            cmbVelocidad.Name = "cmbVelocidad";
            cmbVelocidad.Size = new Size(84, 28);
            cmbVelocidad.TabIndex = 3;
            // 
            // cmbPuerto
            // 
            cmbPuerto.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            cmbPuerto.FormattingEnabled = true;
            cmbPuerto.Location = new Point(447, 285);
            cmbPuerto.Margin = new Padding(3, 4, 3, 4);
            cmbPuerto.Name = "cmbPuerto";
            cmbPuerto.Size = new Size(84, 28);
            cmbPuerto.TabIndex = 4;
            // 
            // btnConectar
            // 
            btnConectar.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            btnConectar.Location = new Point(447, 321);
            btnConectar.Margin = new Padding(3, 4, 3, 4);
            btnConectar.Name = "btnConectar";
            btnConectar.Size = new Size(87, 32);
            btnConectar.TabIndex = 5;
            btnConectar.Text = "Conectar";
            btnConectar.UseVisualStyleBackColor = true;
            btnConectar.Click += btnConectar_Click;
            // 
            // btnEnviarArchivo
            // 
            btnEnviarArchivo.Anchor = AnchorStyles.Right;
            btnEnviarArchivo.Location = new Point(517, 93);
            btnEnviarArchivo.Margin = new Padding(3, 4, 3, 4);
            btnEnviarArchivo.Name = "btnEnviarArchivo";
            btnEnviarArchivo.Size = new Size(74, 67);
            btnEnviarArchivo.TabIndex = 6;
            btnEnviarArchivo.Text = "Enviar Archivo";
            btnEnviarArchivo.UseVisualStyleBackColor = true;
            btnEnviarArchivo.Click += button1_Click;
            // 
            // prgArchivo
            // 
            prgArchivo.Anchor = AnchorStyles.Right;
            prgArchivo.Location = new Point(517, 185);
            prgArchivo.Margin = new Padding(3, 4, 3, 4);
            prgArchivo.Name = "prgArchivo";
            prgArchivo.Size = new Size(65, 19);
            prgArchivo.TabIndex = 7;
            // 
            // label1
            // 
            label1.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            label1.AutoSize = true;
            label1.Location = new Point(249, 244);
            label1.Name = "label1";
            label1.Size = new Size(192, 20);
            label1.TabIndex = 8;
            label1.Text = "Configuracion de Conexion:";
            // 
            // Form1
            // 
            AutoScaleDimensions = new SizeF(8F, 20F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(603, 443);
            Controls.Add(label1);
            Controls.Add(prgArchivo);
            Controls.Add(btnEnviarArchivo);
            Controls.Add(btnConectar);
            Controls.Add(cmbPuerto);
            Controls.Add(cmbVelocidad);
            Controls.Add(rchConversacion);
            Controls.Add(btnEnviaMensaje);
            Controls.Add(txtMensaje);
            Margin = new Padding(3, 4, 3, 4);
            Name = "Form1";
            Text = "Form1";
            Load += Form1_Load;
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private TextBox txtMensaje;
        private Button btnEnviaMensaje;
        private RichTextBox rchConversacion;
        private ComboBox cmbVelocidad;
        private ComboBox cmbPuerto;
        private Button btnConectar;
        private Button btnEnviarArchivo;
        private ProgressBar prgArchivo;
        private Label label1;
    }
}
