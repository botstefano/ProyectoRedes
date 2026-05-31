namespace winProyComunicacion
{
    using System.IO.Ports;
    using System.Drawing;
    using System.Windows.Forms;
    public partial class Form1 : Form
    {
        classComunicacion Enlace;

        private delegate void AccesoControl(string mens);
        private AccesoControl MostrarMensaje;

        public Form1()
        {
            InitializeComponent();
            Enlace = new classComunicacion();
            MostrarMensaje = new AccesoControl(MostrandoMensaje);
            this.MinimumSize = new Size(700, 500);
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            Enlace.llegoMensaje += Enlace_llegoMensaje;
            Enlace.progreso += Enlace_progreso;

            // 2 Lentas
            cmbVelocidad.Items.Add("2400");
            cmbVelocidad.Items.Add("9600");

            // 2 Intermedias
            cmbVelocidad.Items.Add("19200");
            cmbVelocidad.Items.Add("38400");

            // 3 Rápidas
            cmbVelocidad.Items.Add("57600");
            cmbVelocidad.Items.Add("115200");
            cmbVelocidad.Items.Add("230400");

            cmbVelocidad.SelectedIndex = 4;

            string[] puertos = SerialPort.GetPortNames();

            cmbPuerto.Items.AddRange(puertos);

            if (cmbPuerto.Items.Count > 0)
            {
                cmbPuerto.SelectedIndex = 0;
            }
        }

        /*
        private void Form1_Load(object sender, EventArgs e)
        {

            Enlace.llegoMensaje += Enlace_llegoMensaje;
            Enlace.InicializaPuerto("COM5", 115200);
        }
        */
        private void Enlace_llegoMensaje(string m)
        {
            // throw new NotImplementedException();
            Invoke(MostrarMensaje, m);
        }

        private void Enlace_progreso(int p)
        {
            Invoke(new Action(() =>
            {
                prgArchivo.Value = p;
            }));
        }

        private void MostrandoMensaje(string mensaje)
        {
            if (mensaje.StartsWith("[IMG]"))
            {
                MostrarImagen(
                    mensaje.Substring(5));

                return;
            }

            AgregarMensajeFormateado(
                "OTRO",
                mensaje,
                Color.ForestGreen,
                HorizontalAlignment.Left);
        }

        private void MostrarImagen(string ruta)
        {
            Form visor = new Form();

            PictureBox pb =
                new PictureBox();

            pb.Dock = DockStyle.Fill;

            pb.Image =
                Image.FromFile(ruta);

            pb.SizeMode =
                PictureBoxSizeMode.Zoom;

            visor.Controls.Add(pb);

            visor.Width = 600;
            visor.Height = 400;

            visor.Show();
        }

        private void btnEnviaMensaje_Click(object sender, EventArgs e)
        {
            if (!Enlace.sPuerto.IsOpen)
            {
                MessageBox.Show("Primero conecta el puerto");
                return;
            }

            string mensaje = txtMensaje.Text.Trim();

            // VALIDACIÓN: Si el mensaje está vacío o son puros espacios, no hace nada
            if (string.IsNullOrWhiteSpace(mensaje))
            {
                txtMensaje.Clear(); // Limpiamos por si el usuario tecleó puros espacios
                txtMensaje.Focus();
                return;
            }

            Enlace.enviarMensaje(mensaje);

            // Usamos nuestra nueva función: "YO" en Azul y a la Derecha
            AgregarMensajeFormateado("YO", mensaje, Color.Blue, HorizontalAlignment.Right);

            txtMensaje.Clear();
            txtMensaje.Focus(); // Para seguir escribiendo sin tocar el mouse
        }

        private void AgregarMensajeFormateado(string remitente, string mensaje, Color colorRemitente, HorizontalAlignment alineacion)
        {
            // 1. Movemos el cursor al final del texto actual
            rchConversacion.SelectionStart = rchConversacion.TextLength;
            rchConversacion.SelectionLength = 0;

            // 2. Alineamos (Izquierda o Derecha)
            rchConversacion.SelectionAlignment = alineacion;

            // 3. Escribimos el Nombre (Ej: "YO:") en Negrita y con color
            rchConversacion.SelectionColor = colorRemitente;
            rchConversacion.SelectionFont = new Font(rchConversacion.Font, FontStyle.Bold);
            rchConversacion.SelectedText = remitente + ":" + Environment.NewLine;

            // 4. Escribimos el Mensaje en texto normal y color negro
            rchConversacion.SelectionColor = Color.Black;
            rchConversacion.SelectionFont = new Font(rchConversacion.Font, FontStyle.Regular);
            rchConversacion.SelectedText = mensaje + Environment.NewLine + Environment.NewLine;

            // 5. Hacemos scroll automático hacia abajo
            rchConversacion.ScrollToCaret();
        }


        private void txtMensaje_TextChanged(object sender, EventArgs e)
        {

        }

        private void rchConversacion_TextChanged(object sender, EventArgs e)
        {

        }

        private void comboBox1_SelectedIndexChanged(object sender, EventArgs e)
        {

        }

        private void comboBox1_SelectedIndexChanged_1(object sender, EventArgs e)
        {

        }

        private void btnConectar_Click(object sender, EventArgs e)
        {
            try
            {
                string puerto = cmbPuerto.Text;
                int velocidad = Convert.ToInt32(cmbVelocidad.Text);

                Enlace.InicializaPuerto(puerto, velocidad);

                MessageBox.Show("Conectado correctamente");
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        private void txtMensaje_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                e.SuppressKeyPress = true;

                btnEnviaMensaje.PerformClick();
            }
        }

        private void button1_Click(object sender, EventArgs e)
        {
            if (!Enlace.sPuerto.IsOpen)
            {
                MessageBox.Show(
                    "Primero conecta el puerto");

                return;
            }

            OpenFileDialog abrir =
                new OpenFileDialog();

            abrir.Filter =
                "Todos los archivos|*.*";

            if (abrir.ShowDialog()
                == DialogResult.OK)
            {
                prgArchivo.Value = 0;
                Enlace.EnviarArchivo(
                    abrir.FileName);

                AgregarMensajeFormateado(
                    "YO",
                    "Archivo enviado: " +
                    Path.GetFileName(
                        abrir.FileName),
                    Color.Blue,
                    HorizontalAlignment.Right);
            }
        }

        private void progressBar1_Click(object sender, EventArgs e)
        {

        }

        private void label1_Click(object sender, EventArgs e)
        {

        }

        private void label1_Click_1(object sender, EventArgs e)
        {

        }
    }
}
