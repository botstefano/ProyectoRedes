namespace winProyComunicacion
{
    using System.IO.Ports;
    using System.Drawing;
    using System.Windows.Forms;
    using System.IO;

    public partial class Form1 : Form
    {
        classComunicacion Enlace;

        private delegate void AccesoControl(string mens);
        private AccesoControl MostrarMensaje;
        private bool conectado = false;
        private int velocidadActual = 0;

        public Form1()
        {
            InitializeComponent();
            Enlace = new classComunicacion();
            MostrarMensaje = new AccesoControl(MostrandoMensaje);
            this.FormClosing += Form1_FormClosing;
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            try
            {
                Enlace.DetenerEnvio();
                Enlace.CerrarPuerto();
            }
            catch (Exception ex)
            {
                // Silenciar errores al cerrar
            }
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            Enlace.llegoMensaje += Enlace_llegoMensaje;
            Enlace.progreso += Enlace_progreso;
            Enlace.handshakeResultado += Enlace_handshakeResultado;
            Enlace.onSolicitudGuardado += Enlace_onSolicitudGuardado;
            Enlace.onColaActualizada += Enlace_onColaActualizada;
            Enlace.progresoConCola += Enlace_progresoConCola;

            // 2 Lentas
            cmbVelocidad.Items.Add("2400");
            cmbVelocidad.Items.Add("9600");

            // 2 Intermedias
            cmbVelocidad.Items.Add("19200");
            cmbVelocidad.Items.Add("38400");

            // 3 Rápidas estándar
            cmbVelocidad.Items.Add("57600");
            cmbVelocidad.Items.Add("115200");
            cmbVelocidad.Items.Add("230400");

            // Velocidades muy altas (dependen del hardware)
            cmbVelocidad.Items.Add("460800");
            cmbVelocidad.Items.Add("921600");
            cmbVelocidad.Items.Add("1000000");
            cmbVelocidad.Items.Add("2000000");

            cmbVelocidad.SelectedIndex = 4;

            string[] puertos = SerialPort.GetPortNames();
            cmbPuerto.Items.AddRange(puertos);

            if (cmbPuerto.Items.Count > 0)
                cmbPuerto.SelectedIndex = 0;
        }

        private void Enlace_llegoMensaje(string m)
        {
            this.BeginInvoke(MostrarMensaje, m);
        }

        private void Enlace_progreso(int p)
        {
            Invoke(new Action(() =>
            {
                prgArchivo.Value = p;
            }));
        }

        private void Enlace_handshakeResultado(bool exito, int velocidadRemota)
        {
            this.BeginInvoke(new Action(() =>
            {
                if (exito)
                {
                    AgregarMensajeFormateado("SISTEMA", "Handshake exitoso - Velocidad: " + velocidadRemota, Color.Green, HorizontalAlignment.Left);
                }
                else
                {
                    AgregarMensajeFormateado("SISTEMA", "Handshake fallido - Desconectando...", Color.Red, HorizontalAlignment.Left);
                    MessageBox.Show("Las velocidades no coinciden. Local: " + velocidadActual + ", Remota: " + velocidadRemota);

                    Task.Run(() =>
                    {
                        Thread.Sleep(100);
                        this.BeginInvoke(new Action(() => btnConectar.PerformClick()));
                    });
                }
            }));
        }

        private void Enlace_onSolicitudGuardado(string nombreArchivo, long tamaño)
        {
            Invoke(new Action(() =>
            {
                DialogResult dialogResult = MessageBox.Show(
                    $"Alguien quiere enviarte el archivo:\n\n{nombreArchivo}\n\nTamaño: {tamaño / 1024.0 / 1024.0:F2} MB\n\n¿Deseas aceptar la descarga?",
                    "Archivo Entrante", MessageBoxButtons.YesNo, MessageBoxIcon.Information);

                if (dialogResult == DialogResult.Yes)
                {
                    SaveFileDialog guardar = new SaveFileDialog();
                    guardar.FileName = nombreArchivo;
                    guardar.Filter = "Todos los archivos|*.*";
                    guardar.Title = "Selecciona dónde guardar el archivo";

                    if (guardar.ShowDialog() == DialogResult.OK)
                    {
                        Enlace.EstablecerRutaGuardado(guardar.FileName);
                        AgregarMensajeFormateado("SISTEMA", "Descarga aceptada. Guardando en: " + guardar.FileName, Color.Blue, HorizontalAlignment.Left);
                        return;
                    }
                }

                Enlace.EstablecerRutaGuardado(null);
                AgregarMensajeFormateado("SISTEMA", "Rechazaste la recepción de: " + nombreArchivo, Color.Orange, HorizontalAlignment.Left);
            }));
        }

        private void MostrandoMensaje(string mensaje)
        {
            if (mensaje.StartsWith("[IMG]"))
            {
                MostrarImagen(mensaje.Substring(5));
                return;
            }

            if (mensaje.StartsWith("[VIDEO]"))
            {
                AbrirArchivo(mensaje.Substring(7), "video");
                return;
            }

            if (mensaje.StartsWith("[AUDIO]"))
            {
                AbrirArchivo(mensaje.Substring(7), "audio");
                return;
            }

            if (mensaje.StartsWith("[Archivo recibido]"))
            {
                AgregarMensajeFormateado("OTRO", mensaje, Color.ForestGreen, HorizontalAlignment.Left);
                return;
            }

            AgregarMensajeFormateado("OTRO", mensaje, Color.ForestGreen, HorizontalAlignment.Left);
        }

        private void MostrarImagen(string ruta)
        {
            if (!File.Exists(ruta))
            {
                MessageBox.Show("El archivo de imagen no existe");
                return;
            }

            try
            {
                Form visor = new Form();
                PictureBox pb = new PictureBox();
                pb.Dock = DockStyle.Fill;
                pb.SizeMode = PictureBoxSizeMode.Zoom;

                // Método infalible: Leer los bytes a la memoria RAM y soltar el archivo
                byte[] bytesImagen = File.ReadAllBytes(ruta);
                using (MemoryStream ms = new MemoryStream(bytesImagen))
                {
                    pb.Image = Image.FromStream(ms);
                }

                visor.Controls.Add(pb);
                visor.Width = 600;
                visor.Height = 400;
                visor.Show();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al cargar imagen: {ex.Message}");
            }
        }

        private void AbrirArchivo(string ruta, string tipo)
        {
            if (!File.Exists(ruta))
            {
                MessageBox.Show($"El archivo de {tipo} no existe");
                return;
            }

            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = ruta,
                    UseShellExecute = true
                });

                AgregarMensajeFormateado("SISTEMA", $"Abriendo {tipo} con programa predeterminado", Color.Gray, HorizontalAlignment.Left);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al abrir {tipo}: {ex.Message}");
            }
        }

        private void btnEnviaMensaje_Click(object sender, EventArgs e)
        {
            if (Enlace.sPuerto == null || !Enlace.sPuerto.IsOpen)
            {
                MessageBox.Show("Primero conecta el puerto");
                return;
            }

            string mensaje = txtMensaje.Text.Trim();

            if (string.IsNullOrWhiteSpace(mensaje))
            {
                txtMensaje.Clear();
                txtMensaje.Focus();
                return;
            }

            Enlace.enviarMensaje(mensaje);
            AgregarMensajeFormateado("YO", mensaje, Color.Blue, HorizontalAlignment.Right);
            txtMensaje.Clear();
            txtMensaje.Focus();
        }

        private void AgregarMensajeFormateado(string remitente, string mensaje, Color colorRemitente, HorizontalAlignment alineacion)
        {
            rchConversacion.SelectionStart = rchConversacion.TextLength;
            rchConversacion.SelectionLength = 0;
            rchConversacion.SelectionAlignment = alineacion;

            string hora = DateTime.Now.ToString("HH:mm");

            rchConversacion.SelectionColor = colorRemitente;
            rchConversacion.SelectionFont = new Font(rchConversacion.Font, FontStyle.Bold);
            rchConversacion.SelectedText = remitente + " [" + hora + "]:" + Environment.NewLine;

            rchConversacion.SelectionColor = Color.Black;
            rchConversacion.SelectionFont = new Font(rchConversacion.Font, FontStyle.Regular);
            rchConversacion.SelectedText = mensaje + Environment.NewLine + Environment.NewLine;

            rchConversacion.ScrollToCaret();
        }

        private void btnConectar_Click(object sender, EventArgs e)
        {
            try
            {
                if (!conectado)
                {
                    if (string.IsNullOrWhiteSpace(cmbPuerto.Text))
                    {
                        MessageBox.Show("Seleccione un puerto");
                        return;
                    }

                    if (string.IsNullOrWhiteSpace(cmbVelocidad.Text))
                    {
                        MessageBox.Show("Seleccione una velocidad");
                        return;
                    }

                    string puerto = cmbPuerto.Text;
                    int velocidad = Convert.ToInt32(cmbVelocidad.Text);
                    velocidadActual = velocidad;

                    Enlace.InicializaPuerto(puerto, velocidad);
                    Enlace.enviarHandshake(velocidad);

                    conectado = true;
                    btnConectar.Text = "Desconectar";
                    cmbPuerto.Enabled = false;
                    cmbVelocidad.Enabled = false;

                    AgregarMensajeFormateado("SISTEMA", "Conectado. Esperando handshake...", Color.Gray, HorizontalAlignment.Left);
                }
                else
                {
                    Enlace.DetenerEnvio();
                    Enlace.CerrarPuerto();

                    conectado = false;
                    btnConectar.Text = "Conectar";
                    cmbPuerto.Enabled = true;
                    cmbVelocidad.Enabled = true;

                    AgregarMensajeFormateado("SISTEMA", "Desconectado", Color.Gray, HorizontalAlignment.Left);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);

                if (conectado)
                {
                    conectado = false;
                    btnConectar.Text = "Conectar";
                    cmbPuerto.Enabled = true;
                    cmbVelocidad.Enabled = true;
                }
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

        private string CalcularTiempoEstimado(long tamañoBytes, int velocidadBaudios)
        {
            double velocidadBytesPorSeg = velocidadBaudios / 10.0;
            double tiempoSegundos = tamañoBytes / velocidadBytesPorSeg;

            if (tiempoSegundos < 60)
                return $"{tiempoSegundos:F1} segundos";
            else if (tiempoSegundos < 3600)
                return $"{tiempoSegundos / 60:F1} minutos";
            else
                return $"{tiempoSegundos / 3600:F1} horas";
        }

        private void button1_Click(object sender, EventArgs e)
        {
            if (Enlace.sPuerto == null || !Enlace.sPuerto.IsOpen)
            {
                MessageBox.Show("Primero conecta el puerto");
                return;
            }

            OpenFileDialog abrir = new OpenFileDialog();
            abrir.Filter = "Todos los archivos|*.*";
            abrir.Multiselect = true; // Permitir selección múltiple

            if (abrir.ShowDialog() == DialogResult.OK)
            {
                foreach (string archivo in abrir.FileNames)
                {
                    FileInfo info = new FileInfo(archivo);
                    long tamaño = info.Length;
                    string tiempoEstimado = CalcularTiempoEstimado(tamaño, velocidadActual);

                    DialogResult resultado = DialogResult.Yes;

                    if (tamaño > 1024 * 1024)
                    {
                        string mensaje = $"El archivo tiene un tamaño de {tamaño / 1024.0 / 1024.0:F2} MB.\n" +
                                         $"Tiempo estimado de transferencia: {tiempoEstimado}\n" +
                                         $"¿Desea continuar?";
                        resultado = MessageBox.Show(mensaje, "Advertencia: Archivo grande",
                                                    MessageBoxButtons.YesNo, MessageBoxIcon.Warning);

                        if (resultado != DialogResult.Yes)
                            continue;
                    }

                    prgArchivo.Value = 0;
                    Enlace.EnviarArchivo(archivo);

                    AgregarMensajeFormateado(
                        "YO",
                        $"Archivo enviado: {Path.GetFileName(archivo)} ({tamaño / 1024.0 / 1024.0:F2} MB, Tiempo estimado: {tiempoEstimado})",
                        Color.Blue,
                        HorizontalAlignment.Right);
                }
            }
        }

        private void Enlace_onColaActualizada(int pendientes, int enviando, int completados)
        {
            if (InvokeRequired)
            {
                Invoke(new Action<int, int, int>(Enlace_onColaActualizada), pendientes, enviando, completados);
                return;
            }

            string estadoCola = $"[Cola] Pendientes: {pendientes} | Enviando: {enviando} | Completados: {completados}";
            AgregarMensajeFormateado("SISTEMA", estadoCola, Color.Gray, HorizontalAlignment.Center);
        }

        private void Enlace_progresoConCola(int porcentajeTotal, long totalEnviado, long totalCola)
        {
            if (InvokeRequired)
            {
                Invoke(new Action<int, long, long>(Enlace_progresoConCola), porcentajeTotal, totalEnviado, totalCola);
                return;
            }

            // Actualizar la progress bar con el progreso total de la cola
            prgArchivo.Value = porcentajeTotal;
        }

        private void rchConversacion_TextChanged(object sender, EventArgs e)
        {

        }
    }
}