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
            this.MinimumSize = new Size(700, 500);
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

        private void Enlace_handshakeResultado(bool exito, int velocidadRemota)
        {
            Invoke(new Action(() =>
            {
                if (exito)
                {
                    AgregarMensajeFormateado("SISTEMA", "Handshake exitoso - Velocidad: " + velocidadRemota, Color.Green, HorizontalAlignment.Left);
                }
                else
                {
                    AgregarMensajeFormateado("SISTEMA", "Handshake fallido - Desconectando...", Color.Red, HorizontalAlignment.Left);
                    MessageBox.Show("Las velocidades no coinciden. Local: " + velocidadActual + ", Remota: " + velocidadRemota);
                    btnConectar.PerformClick(); // Desconectar
                }
            }));
        }

        private void Enlace_onSolicitudGuardado(string nombreArchivo, long tamaño)
        {
            Invoke(new Action(() =>
            {
                SaveFileDialog guardar = new SaveFileDialog();
                guardar.FileName = nombreArchivo;
                guardar.Filter = "Todos los archivos|*.*";
                guardar.Title = $"Guardar archivo recibido ({tamaño / 1024.0:F2} KB)";

                if (guardar.ShowDialog() == DialogResult.OK)
                {
                    Enlace.EstablecerRutaGuardado(guardar.FileName);
                    AgregarMensajeFormateado("SISTEMA", "Guardando archivo en: " + guardar.FileName, Color.Blue, HorizontalAlignment.Left);
                }
                else
                {
                    Enlace.EstablecerRutaGuardado(null); // Cancelar
                    AgregarMensajeFormateado("SISTEMA", "Guardado de archivo cancelado", Color.Orange, HorizontalAlignment.Left);
                }
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

            if (mensaje.StartsWith("[VIDEO]"))
            {
                AbrirArchivo(
                    mensaje.Substring(7),
                    "video");
                return;
            }

            if (mensaje.StartsWith("[AUDIO]"))
            {
                AbrirArchivo(
                    mensaje.Substring(7),
                    "audio");
                return;
            }

            if (mensaje.StartsWith("[Archivo recibido]"))
            {
                // Extraer nombre del archivo
                string nombreArchivo = mensaje.Substring("[Archivo recibido] ".Length);
                AgregarMensajeFormateado(
                    "OTRO",
                    mensaje,
                    Color.ForestGreen,
                    HorizontalAlignment.Left);
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
            if (!File.Exists(ruta))
            {
                MessageBox.Show("El archivo de imagen no existe");
                return;
            }

            try
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
                // Abrir con el programa predeterminado del sistema
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

            // 3. Obtenemos hora acortada
            string hora = DateTime.Now.ToString("HH:mm");

            // 4. Escribimos el Nombre (Ej: "YO:") en Negrita y con color
            rchConversacion.SelectionColor = colorRemitente;
            rchConversacion.SelectionFont = new Font(rchConversacion.Font, FontStyle.Bold);
            rchConversacion.SelectedText = remitente + " [" + hora + "]:" + Environment.NewLine;

            // 5. Escribimos el Mensaje en texto normal y color negro
            rchConversacion.SelectionColor = Color.Black;
            rchConversacion.SelectionFont = new Font(rchConversacion.Font, FontStyle.Regular);
            rchConversacion.SelectedText = mensaje + Environment.NewLine + Environment.NewLine;

            // 6. Hacemos scroll automático hacia abajo
            rchConversacion.ScrollToCaret();
        }



        private void btnConectar_Click(object sender, EventArgs e)
        {
            try
            {
                if (!conectado)
                {
                    // Conectar
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

                    // Enviar handshake con velocidad
                    Enlace.enviarHandshake(velocidad);

                    conectado = true;
                    btnConectar.Text = "Desconectar";
                    cmbPuerto.Enabled = false;
                    cmbVelocidad.Enabled = false;

                    AgregarMensajeFormateado("SISTEMA", "Conectado. Esperando handshake...", Color.Gray, HorizontalAlignment.Left);
                }
                else
                {
                    // Desconectar
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
            // Calcular velocidad efectiva (aprox 10 bits por byte debido a start/stop bits)
            double velocidadBytesPorSeg = velocidadBaudios / 10.0;
            double tiempoSegundos = tamañoBytes / velocidadBytesPorSeg;
            
            if (tiempoSegundos < 60)
            {
                return $"{tiempoSegundos:F1} segundos";
            }
            else if (tiempoSegundos < 3600)
            {
                double minutos = tiempoSegundos / 60;
                return $"{minutos:F1} minutos";
            }
            else
            {
                double horas = tiempoSegundos / 3600;
                return $"{horas:F1} horas";
            }
        }

        private void button1_Click(object sender, EventArgs e)
        {
            if (Enlace.sPuerto == null || !Enlace.sPuerto.IsOpen)
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
                FileInfo info = new FileInfo(abrir.FileName);
                long tamaño = info.Length;
                
                // Calcular tiempo estimado
                string tiempoEstimado = CalcularTiempoEstimado(tamaño, velocidadActual);
                
                // Mostrar advertencia para archivos grandes
                DialogResult resultado = DialogResult.Yes;
                if (tamaño > 1024 * 1024) // Más de 1 MB
                {
                    string mensaje = $"El archivo tiene un tamaño de {tamaño / 1024.0 / 1024.0:F2} MB.\n" +
                                   $"Tiempo estimado de transferencia: {tiempoEstimado}\n" +
                                   $"¿Desea continuar?";
                    resultado = MessageBox.Show(mensaje, "Advertencia: Archivo grande", 
                                                MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
                }
                
                if (resultado == DialogResult.Yes)
                {
                    prgArchivo.Value = 0;
                    Enlace.EnviarArchivo(
                        abrir.FileName);

                    AgregarMensajeFormateado(
                        "YO",
                        "Archivo enviado: " +
                        Path.GetFileName(
                            abrir.FileName) +
                        $" ({tamaño / 1024.0 / 1024.0:F2} MB, Tiempo estimado: {tiempoEstimado})",
                        Color.Blue,
                        HorizontalAlignment.Right);
                }
            }
        }

    }
}
