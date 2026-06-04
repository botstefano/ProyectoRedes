using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO.Ports;
using System.Threading;
using System.IO;

namespace winProyComunicacion
{
    internal class classComunicacion
    {
        public SerialPort sPuerto;

        public delegate void miManejador(string m);
        public event miManejador llegoMensaje;

        public delegate void progresoArchivo(int p);
        public event progresoArchivo progreso;

        public delegate void handshakeCallback(bool exito, int velocidadRemota);
        public event handshakeCallback handshakeResultado;

        public delegate void solicitudGuardadoArchivo(string nombreArchivo, long tamaño);
        public event solicitudGuardadoArchivo onSolicitudGuardado;

        private string rutaGuardadoArchivo = null;
        private bool esperandoRutaGuardado = false;

        private string rutaArchivoEnvio;
        private Thread hebraArchivo;
        private bool enviandoArchivo = false;
        private int velocidadLocal = 0;
        private bool handshakeCompletado = false;
        private bool esperandoRespuestaArchivo = false;
        private bool archivoAceptado = false;

        // Variables para hilo de recepción de archivos
        private Thread hebraRecepcion;
        private bool recibiendoArchivo = false;
        private bool recepcionIniciada = false;

        public classComunicacion()
        {
            sPuerto = new SerialPort();
        }

        public void InicializaPuerto(string nombreP, int velocidad)
        {
            try
            {
                sPuerto.DataReceived += SPuerto_DataReceived;
                sPuerto.PortName = nombreP;
                sPuerto.BaudRate = velocidad;
                sPuerto.DataBits = 8;
                sPuerto.StopBits = StopBits.One;
                sPuerto.Parity = Parity.None;
                sPuerto.ReadBufferSize = 262144; // 256KB
                sPuerto.WriteBufferSize = 262144; // 256KB
                sPuerto.ReadTimeout = 1800000; // 30 minutos para archivos grandes
                sPuerto.WriteTimeout = 1800000; // 30 minutos para archivos grandes
                sPuerto.Open();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al inicializar puerto: {ex.Message}");
            }
        }

        private void SPuerto_DataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            // Ignorar eventos si estamos recibiendo un archivo (manejado por el hilo separado)
            if (recibiendoArchivo)
                return;

            try
            {
                byte[] cabecera = new byte[9];
                int leidos = 0;

                while (leidos < 9)
                {
                    leidos += sPuerto.Read(cabecera, leidos, 9 - leidos);
                }

                string tipo = Encoding.UTF8.GetString(cabecera, 0, 1);

                int longitud;
                try
                {
                    string longitudStr = Encoding.UTF8.GetString(cabecera, 1, 8);
                    onLlegomensaje($"[DEBUG] Tipo: {tipo}, Longitud recibida: '{longitudStr}'");
                    longitud = Convert.ToInt32(longitudStr);
                }
                catch (FormatException)
                {
                    string longitudStr = Encoding.UTF8.GetString(cabecera, 1, 8);
                    onLlegomensaje($"[Error] Formato de longitud inválido en cabecera: '{longitudStr}'");
                    return;
                }

                switch (tipo)
                {
                    case "H":
                        RecibiendoHandshake(longitud);
                        break;

                    case "M":
                        RecibiendoMensaje(longitud);
                        break;

                    case "A":
                        // Iniciar hilo separado para recepción de archivos
                        if (!recibiendoArchivo)
                        {
                            recibiendoArchivo = true;
                            hebraRecepcion = new Thread(() => RecibiendoArchivoEnHilo(longitud));
                            hebraRecepcion.IsBackground = true;
                            hebraRecepcion.Start();
                        }
                        break;

                    case "F":
                        RecibiendoRespuestaArchivo(longitud);
                        break;
                }
            }
            catch (Exception ex)
            {
                onLlegomensaje($"[Error al recibir datos] {ex.Message}");
            }
        }

        private void RecibiendoMensaje(int longitud)
        {
            byte[] datos = new byte[longitud];
            int total = 0;
            int intentos = 0;
            const int MAX_INTENTOS = 10;

            while (total < longitud && intentos < MAX_INTENTOS)
            {
                try
                {
                    int leidos = sPuerto.Read(datos, total, longitud - total);

                    if (leidos <= 0)
                    {
                        intentos++;
                        Thread.Sleep(100);
                        continue;
                    }

                    total += leidos;
                    intentos = 0;
                }
                catch (TimeoutException)
                {
                    intentos++;
                    Thread.Sleep(100);
                }
            }

            if (total < longitud)
            {
                onLlegomensaje("[Error] Timeout recibiendo mensaje");
                return;
            }

            string mensaje = Encoding.UTF8.GetString(datos);
            onLlegomensaje(mensaje);
        }

        private void RecibiendoArchivoEnHilo(int longitudCabecera)
        {
            try
            {
                byte[] datosCabecera = new byte[longitudCabecera];
                int totalCabecera = 0;

                while (totalCabecera < longitudCabecera)
                {
                    totalCabecera += sPuerto.Read(datosCabecera, totalCabecera, longitudCabecera - totalCabecera);
                }

                // Validar longitud mínima (4 bytes longitud + 8 bytes tamaño = 12 bytes mínimo)
                if (datosCabecera.Length < 12)
                    throw new Exception("Cabecera de archivo demasiado corta");

                // Formato binario: [4 bytes longitud nombre][nombre][8 bytes tamaño]
                int longitudNombre = BitConverter.ToInt32(datosCabecera, 0);

                // Validar que la longitud del nombre es razonable
                if (longitudNombre < 0 || longitudNombre > 255)
                    throw new Exception("Longitud de nombre de archivo inválida");

                // Validar que hay suficientes bytes para leer el nombre y el tamaño
                if (datosCabecera.Length < 4 + longitudNombre + 8)
                    throw new Exception("Cabecera de archivo incompleta");

                string nombre = Encoding.UTF8.GetString(datosCabecera, 4, longitudNombre);
                long tamaño = BitConverter.ToInt64(datosCabecera, 4 + longitudNombre);

                esperandoRutaGuardado = true;
                onSolicitudGuardado(nombre, tamaño);

                int timeoutEspera = 0;
                while (esperandoRutaGuardado && timeoutEspera < 600)
                {
                    Thread.Sleep(100);
                    timeoutEspera++;
                }

                bool aceptado = false;
                string ruta = rutaGuardadoArchivo;
                rutaGuardadoArchivo = null;
                FileStream fs = null;
                BinaryWriter binaryWriter = null;

                if (!esperandoRutaGuardado && !string.IsNullOrEmpty(ruta))
                {
                    try
                    {
                        fs = new FileStream(ruta, FileMode.Create);
                        binaryWriter = new BinaryWriter(fs);
                        aceptado = true;
                    }
                    catch (Exception ex)
                    {
                        onLlegomensaje("[Error de Sistema] No se pudo sobrescribir el archivo. Puede que esté abierto o bloqueado: " + ex.Message);
                        aceptado = false;
                    }
                }

                EnviarRespuestaArchivo(aceptado);

                if (!aceptado)
                    return;

                // 3. RECIBIMOS LOS DATOS (Ya tenemos el FileStream y BinaryWriter abiertos y listos)
                try
                {
                    byte[] buffer = new byte[4096];
                    long recibidos = 0;
                    int intentosSinDatos = 0;
                    const int MAX_INTENTOS = 3000; // 5 minutos (3000 * 100ms)

                    // Variable para no saturar la interfaz
                    int ultimoPorcentaje = -1;

                    while (recibidos < tamaño && recibiendoArchivo)
                    {
                        // Verificar si hay datos disponibles antes de leer
                        if (sPuerto.BytesToRead > 0)
                        {
                            int faltan = (int)Math.Min(buffer.Length, tamaño - recibidos);
                            // No leer más de lo disponible en el buffer
                            faltan = (int)Math.Min(faltan, sPuerto.BytesToRead);
                            int leer = sPuerto.Read(buffer, 0, faltan);

                            if (leer > 0)
                            {
                                binaryWriter.Write(buffer, 0, leer);
                                recibidos += leer;
                                intentosSinDatos = 0;

                                int porcentaje = (int)((recibidos * 100) / tamaño);

                                // Solo actualizamos la barra si el número cambió
                                if (porcentaje != ultimoPorcentaje)
                                {
                                    onProgreso(porcentaje);
                                    ultimoPorcentaje = porcentaje;
                                }
                            }
                        }
                        else
                        {
                            intentosSinDatos++;
                            if (intentosSinDatos > MAX_INTENTOS)
                                throw new Exception("Timeout leyendo datos");
                            Thread.Sleep(100);
                        }
                    }
                }
                finally
                {
                    // Cerrar el BinaryWriter y FileStream
                    binaryWriter?.Dispose();
                    fs?.Dispose();

                    // Limpiamos la tubería de entrada de cualquier residuo fantasma
                    if (sPuerto != null && sPuerto.IsOpen)
                    {
                        sPuerto.DiscardInBuffer();
                    }
                }

                onLlegomensaje("[Archivo recibido] " + nombre);

                string extension = Path.GetExtension(nombre).ToLower();
                string[] extensionesImagen = { ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".ico", ".webp", ".tiff", ".tif" };
                string[] extensionesVideo = { ".mp4", ".avi", ".mkv", ".mov", ".wmv", ".flv", ".webm", ".m4v" };
                string[] extensionesAudio = { ".mp3", ".wav", ".ogg", ".flac", ".aac", ".m4a", ".wma" };

                if (extensionesImagen.Contains(extension))
                    onLlegomensaje("[IMG]" + ruta);
                else if (extensionesVideo.Contains(extension))
                    onLlegomensaje("[VIDEO]" + ruta);
                else if (extensionesAudio.Contains(extension))
                    onLlegomensaje("[AUDIO]" + ruta);
            }
            catch (Exception ex)
            {
                onLlegomensaje("[Error al recibir archivo] " + ex.Message);
            }
            finally
            {
                recibiendoArchivo = false;
            }
        }

        protected virtual void onLlegomensaje(string m)
        {
            llegoMensaje?.Invoke(m);
        }

        protected virtual void onHandshakeResultado(bool exito, int velocidad)
        {
            handshakeResultado?.Invoke(exito, velocidad);
        }

        public void enviarHandshake(int velocidad)
        {
            velocidadLocal = velocidad;
            string velocidadStr = velocidad.ToString("D8");
            byte[] datos = Encoding.UTF8.GetBytes(velocidadStr);
            string longitud = datos.Length.ToString("D8");
            byte[] cabecera = Encoding.UTF8.GetBytes("H" + longitud);

            if (!sPuerto.IsOpen)
            {
                onLlegomensaje("[Error] El puerto no está abierto");
                return;
            }

            sPuerto.Write(cabecera, 0, cabecera.Length);
            sPuerto.Write(datos, 0, datos.Length);
        }

        private void RecibiendoHandshake(int longitud)
        {
            try
            {
                byte[] datos = new byte[longitud];
                int total = 0;

                while (total < longitud)
                {
                    int leer = sPuerto.Read(datos, total, longitud - total);

                    if (leer <= 0)
                        throw new Exception("Timeout recibiendo handshake");

                    total += leer;
                }

                string velocidadStr = Encoding.UTF8.GetString(datos);

                if (int.TryParse(velocidadStr, out int velocidadRemota))
                {
                    if (velocidadRemota == velocidadLocal)
                    {
                        onHandshakeResultado(true, velocidadRemota);
                        onLlegomensaje("[Handshake exitoso] Velocidad coincidente: " + velocidadRemota);
                    }
                    else
                    {
                        onHandshakeResultado(false, velocidadRemota);
                        onLlegomensaje("[Handshake fallido] Velocidad no coincide: Local=" + velocidadLocal + ", Remota=" + velocidadRemota);
                    }
                }
                else
                {
                    onHandshakeResultado(false, 0);
                    onLlegomensaje("[Handshake fallido] Formato inválido");
                }
            }
            catch (Exception ex)
            {
                onHandshakeResultado(false, 0);
                onLlegomensaje("[Error en handshake] " + ex.Message);
            }
        }

        public void enviarMensaje(string m)
        {
            if (!sPuerto.IsOpen)
            {
                onLlegomensaje("[Error] El puerto no está abierto");
                return;
            }

            byte[] datos = Encoding.UTF8.GetBytes(m);
            string longitud = datos.Length.ToString("D8");
            byte[] cabecera = Encoding.UTF8.GetBytes("M" + longitud);

            sPuerto.Write(cabecera, 0, cabecera.Length);
            sPuerto.Write(datos, 0, datos.Length);
        }

        public void EnviarArchivo(string ruta)
        {
            if (enviandoArchivo)
            {
                onLlegomensaje("[Error] Ya se está enviando un archivo");
                return;
            }

            if (!File.Exists(ruta))
            {
                onLlegomensaje("[Error] El archivo no existe");
                return;
            }

            rutaArchivoEnvio = ruta;
            enviandoArchivo = true;

            hebraArchivo = new Thread(EnviandoArchivo);
            hebraArchivo.Start();
        }

        private void EnviandoArchivo()
        {
            try
            {
                // Abrimos y protegemos el archivo primero (FileShare.Read)
                // Esto evita que el Receptor pueda vaciarlo accidentalmente a 0MB
                using (FileStream fs = new FileStream(rutaArchivoEnvio, FileMode.Open, FileAccess.Read, FileShare.Read))
                using (BinaryReader binaryReader = new BinaryReader(fs))
                {
                    FileInfo info = new FileInfo(rutaArchivoEnvio);
                    string nombre = info.Name;
                    long tamaño = fs.Length; // Tomamos el tamaño del archivo ya protegido

                    // Formato binario: [4 bytes longitud nombre][nombre][8 bytes tamaño]
                    byte[] nombreBytes = Encoding.UTF8.GetBytes(nombre);
                    byte[] tamañoBytes = BitConverter.GetBytes(tamaño);

                    byte[] datosCabecera = new byte[4 + nombreBytes.Length + 8];
                    BitConverter.GetBytes(nombreBytes.Length).CopyTo(datosCabecera, 0);
                    nombreBytes.CopyTo(datosCabecera, 4);
                    tamañoBytes.CopyTo(datosCabecera, 4 + nombreBytes.Length);

                    byte[] cabecera = Encoding.UTF8.GetBytes("A" + datosCabecera.Length.ToString("D8"));

                    if (!sPuerto.IsOpen)
                    {
                        onLlegomensaje("[Error] El puerto no está abierto");
                        return;
                    }

                    sPuerto.Write(cabecera, 0, cabecera.Length);
                    sPuerto.Write(datosCabecera, 0, datosCabecera.Length);

                    onLlegomensaje("[SISTEMA] Esperando a que el destinatario acepte el archivo...");

                    esperandoRespuestaArchivo = true;
                    int timeoutEspera = 0;

                    while (esperandoRespuestaArchivo && timeoutEspera < 600)
                    {
                        if (!sPuerto.IsOpen) return;
                        Thread.Sleep(100);
                        timeoutEspera++;
                    }

                    if (esperandoRespuestaArchivo)
                    {
                        onLlegomensaje("[Error] El destinatario no respondió a tiempo. Envío cancelado.");
                        return;
                    }

                    if (!archivoAceptado)
                    {
                        onLlegomensaje("[SISTEMA] El destinatario rechazó el archivo o hubo un bloqueo de sistema.");
                        return;
                    }

                    onLlegomensaje("[SISTEMA] Archivo aceptado. Iniciando transferencia...");

                    byte[] buffer = new byte[4096];
                    int leidos;
                    long enviados = 0;
                    int intentosEscritura = 0;
                    const int MAX_INTENTOS_ESCRITURA = 100;

                    // Variable para no saturar la interfaz
                    int ultimoPorcentaje = -1;

                    // Leemos y enviamos usando BinaryReader
                    while ((leidos = binaryReader.Read(buffer, 0, buffer.Length)) > 0)
                    {
                        if (!sPuerto.IsOpen)
                        {
                            onLlegomensaje("[Error] El puerto se cerró durante el envío");
                            break;
                        }

                        bool escrito = false;
                        while (!escrito && intentosEscritura < MAX_INTENTOS_ESCRITURA)
                        {
                            try
                            {
                                sPuerto.Write(buffer, 0, leidos);
                                escrito = true;
                                intentosEscritura = 0;
                                Thread.Sleep(10); // Aumentado para dar tiempo al receptor a procesar
                            }
                            catch (TimeoutException)
                            {
                                intentosEscritura++;
                                Thread.Sleep(100);
                            }
                        }

                        if (!escrito)
                            throw new Exception("Timeout escribiendo en puerto serie");

                        enviados += leidos;
                        int porcentaje = (int)((enviados * 100) / tamaño);

                        // Solo actualizamos la barra si el número cambió
                        if (porcentaje != ultimoPorcentaje)
                        {
                            onProgreso(porcentaje);
                            ultimoPorcentaje = porcentaje;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                onLlegomensaje($"[Error al enviar archivo] {ex.Message}");
            }
            finally
            {
                enviandoArchivo = false;

                // Limpiamos la tubería de salida al terminar
                if (sPuerto != null && sPuerto.IsOpen)
                {
                    sPuerto.DiscardOutBuffer();
                }
            }
        }

        protected virtual void onProgreso(int p)
        {
            progreso?.Invoke(p);
        }

        public void CerrarPuerto()
        {
            try
            {
                if (sPuerto != null && sPuerto.IsOpen)
                {
                    sPuerto.Close();
                    sPuerto.Dispose();
                }
            }
            catch (Exception ex)
            {
                onLlegomensaje($"[Error al cerrar puerto] {ex.Message}");
            }
        }

        public void EstablecerRutaGuardado(string ruta)
        {
            rutaGuardadoArchivo = ruta;
            esperandoRutaGuardado = false;
        }

        public void DetenerEnvio()
        {
            enviandoArchivo = false;
            recibiendoArchivo = false;
            esperandoRutaGuardado = false;

            if (hebraArchivo != null && hebraArchivo.IsAlive)
            {
                hebraArchivo.Join(1000);
            }

            if (hebraRecepcion != null && hebraRecepcion.IsAlive)
            {
                hebraRecepcion.Join(1000);
            }
        }

        private void EnviarRespuestaArchivo(bool aceptado)
        {
            try
            {
                string resp = aceptado ? "1" : "0";
                byte[] datos = Encoding.UTF8.GetBytes(resp);
                string longitud = datos.Length.ToString("D8");
                byte[] cabecera = Encoding.UTF8.GetBytes("F" + longitud);

                sPuerto.Write(cabecera, 0, cabecera.Length);
                sPuerto.Write(datos, 0, datos.Length);
            }
            catch (Exception ex)
            {
                onLlegomensaje("[Error al responder] " + ex.Message);
            }
        }

        private void RecibiendoRespuestaArchivo(int longitud)
        {
            byte[] datos = new byte[longitud];
            int total = 0;

            while (total < longitud)
            {
                total += sPuerto.Read(datos, total, longitud - total);
            }

            string resp = Encoding.UTF8.GetString(datos);
            archivoAceptado = (resp == "1");
            esperandoRespuestaArchivo = false;
        }

        ~classComunicacion()
        {
            CerrarPuerto();
            DetenerEnvio();
        }
    }
}
