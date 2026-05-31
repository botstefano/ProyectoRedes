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
                sPuerto.ReadBufferSize = 65536;// buffer más grande para archivos grandes
                sPuerto.WriteBufferSize = 65536;
                sPuerto.ReadTimeout = 300000;// 5 minutos timeout para archivos grandes
                sPuerto.WriteTimeout = 300000;
                sPuerto.Open();

            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al inicializar puerto: {ex.Message}");
            }

        }

        private void SPuerto_DataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            try
            {
                byte[] cabecera =
                    new byte[9];

                int leidos = 0;

                while (leidos < 9)
                {
                    leidos += sPuerto.Read(
                        cabecera,
                        leidos,
                        9 - leidos);
                }

                string tipo =
                    Encoding.UTF8.GetString(
                        cabecera,
                        0,
                        1);

                int longitud =
                    Convert.ToInt32(
                        Encoding.UTF8.GetString(
                            cabecera,
                            1,
                            8));

                switch (tipo)
                {
                    case "H":

                        RecibiendoHandshake(
                            longitud);

                        break;

                    case "M":

                        RecibiendoMensaje(
                            longitud);

                        break;

                    case "A":

                        RecibiendoArchivo(
                            longitud);

                        break;
                }
            }
            catch (Exception ex)
            {
                onLlegomensaje($"[Error al recibir datos] {ex.Message}");
            }
        }

        private void RecibiendoMensaje(
    int longitud)
        {
            byte[] datos =
                new byte[longitud];

            int total = 0;
            int intentos = 0;
            const int MAX_INTENTOS = 10;

            while (total < longitud && intentos < MAX_INTENTOS)
            {
                try
                {
                    int leidos = sPuerto.Read(
                        datos,
                        total,
                        longitud - total);
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

            string mensaje =
                Encoding.UTF8.GetString(
                    datos);

            onLlegomensaje(
                mensaje);
        }

        private void RecibiendoArchivo(int longitudCabecera)
        {
            try
            {
                byte[] datosCabecera =
                    new byte[longitudCabecera];

                int totalCabecera = 0;

                while (totalCabecera < longitudCabecera)
                {
                    totalCabecera += sPuerto.Read(
                        datosCabecera,
                        totalCabecera,
                        longitudCabecera - totalCabecera);
                }

                string datos =
                    Encoding.UTF8.GetString(
                        datosCabecera);

                string[] partes =
                    datos.Split('|');

                if (partes.Length < 2)
                {
                    throw new Exception("Formato de cabecera de archivo inválido");
                }

                string nombre =
                    partes[0];

                if (!long.TryParse(partes[1], out long tamaño))
                {
                    throw new Exception("Tamaño de archivo inválido");
                }

                // Solicitar al usuario dónde guardar el archivo
                esperandoRutaGuardado = true;
                onSolicitudGuardado(nombre, tamaño);

                // Esperar a que el usuario seleccione la ruta (máximo 5 minutos)
            
                int timeoutEspera = 0;
                while (esperandoRutaGuardado && timeoutEspera < 3000)
                {
                    Thread.Sleep(100);
                    timeoutEspera++;
                }

                if (esperandoRutaGuardado || string.IsNullOrEmpty(rutaGuardadoArchivo))
                {
                    throw new Exception("No se seleccionó ubicación para guardar el archivo");
                }

                string ruta = rutaGuardadoArchivo;
                rutaGuardadoArchivo = null; // Resetear para próximo archivo

                using (FileStream fs =
                       new FileStream(
                           ruta,
                           FileMode.Create))
                {
                    byte[] buffer =
                        new byte[65536]; // Buffer más grande para archivos grandes

                    long recibidos = 0;
                    int intentosSinDatos = 0;
                    const int MAX_INTENTOS_SIN_DATOS = 500; // Aumentado para archivos grandes

                    while (recibidos < tamaño)
                    {
                        int faltan =
                            (int)Math.Min(
                                buffer.Length,
                                tamaño - recibidos);

                        try
                        {
                            int leer =
                                sPuerto.Read(
                                    buffer,
                                    0,
                                    faltan);

                            if (leer <= 0)
                            {
                                intentosSinDatos++;
                                if (intentosSinDatos >= MAX_INTENTOS_SIN_DATOS)
                                {
                                    throw new Exception(
                                        "Timeout: No llegaron más datos después de múltiples intentos");
                                }
                                Thread.Sleep(100);
                                continue;
                            }

                            intentosSinDatos = 0;
                            fs.Write(
                                buffer,
                                0,
                                leer);

                            recibidos += leer;
                            Console.WriteLine(
                                $"RECIBIDOS: {recibidos}/{tamaño}");

                            onLlegomensaje(
                                $"Recibidos {recibidos}/{tamaño}");

                            int porcentaje =
                                (int)((recibidos * 100)
                                / tamaño);

                            onProgreso(
                                porcentaje);
                        }
                        catch (TimeoutException)
                        {
                            intentosSinDatos++;
                            if (intentosSinDatos >= MAX_INTENTOS_SIN_DATOS)
                            {
                                throw new Exception(
                                    "Timeout: No llegaron más datos después de múltiples intentos");
                            }
                        }
                    }
                }

                onLlegomensaje(
                    "[Archivo recibido] " +
                    nombre);

                // Detectar tipo de archivo y enviar mensaje especial
                string extension = Path.GetExtension(nombre).ToLower();
                string[] extensionesImagen = { ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".ico", ".webp", ".tiff", ".tif" };
                string[] extensionesVideo = { ".mp4", ".avi", ".mkv", ".mov", ".wmv", ".flv", ".webm", ".m4v" };
                string[] extensionesAudio = { ".mp3", ".wav", ".ogg", ".flac", ".aac", ".m4a", ".wma" };
                
                if (extensionesImagen.Contains(extension))
                {
                    onLlegomensaje("[IMG]" + ruta);
                }
                else if (extensionesVideo.Contains(extension))
                {
                    onLlegomensaje("[VIDEO]" + ruta);
                }
                else if (extensionesAudio.Contains(extension))
                {
                    onLlegomensaje("[AUDIO]" + ruta);
                }
            }
            catch (Exception ex)
            {
                onLlegomensaje(
                    "[Error al recibir archivo] " +
                    ex.Message);
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
                    {
                        throw new Exception("Timeout recibiendo handshake");
                    }
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

            byte[] datos =
                Encoding.UTF8.GetBytes(m);

            string longitud =
                datos.Length.ToString("D8");

            byte[] cabecera =
                Encoding.UTF8.GetBytes(
                    "M" + longitud);

            sPuerto.Write(
                cabecera,
                0,
                cabecera.Length);

            sPuerto.Write(
                datos,
                0,
                datos.Length);
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
                FileInfo info = new FileInfo(rutaArchivoEnvio);

                string nombre = info.Name;

                long tamaño = info.Length;

                string cabeceraArchivo =
                    nombre + "|" + tamaño;

                byte[] datosCabecera =
                    Encoding.UTF8.GetBytes(cabeceraArchivo);

                byte[] cabecera =
                    Encoding.UTF8.GetBytes(
                        "A" +
                        datosCabecera.Length.ToString("D8"));

                if (!sPuerto.IsOpen)
                {
                    onLlegomensaje("[Error] El puerto no está abierto");
                    return;
                }

                sPuerto.Write(cabecera, 0, cabecera.Length);

                sPuerto.Write(
                    datosCabecera,
                    0,
                    datosCabecera.Length);



                using (FileStream fs =
                       new FileStream(
                           rutaArchivoEnvio,
                           FileMode.Open,
                           FileAccess.Read))
                {
                    byte[] buffer =
                        new byte[65536]; // Buffer más grande para archivos grandes

                    int leidos;
                    long enviados = 0;
                    int intentosEscritura = 0;
                    const int MAX_INTENTOS_ESCRITURA = 100; // Aumentado para archivos grandes

                    while ((leidos =
                            fs.Read(
                                buffer,
                                0,
                                buffer.Length)) > 0)
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
                                sPuerto.Write(
                                    buffer,
                                    0,
                                    leidos);
                                escrito = true;
                                intentosEscritura = 0;
                            }
                            catch (TimeoutException)
                            {
                                intentosEscritura++;
                                Thread.Sleep(100);
                            }
                        }

                        if (!escrito)
                        {
                            throw new Exception("Timeout escribiendo en puerto serie");
                        }

                        enviados += leidos;
                        Console.WriteLine(
                            $"ENVIADOS: {enviados}/{tamaño}");

                        int porcentaje =
                            (int)((enviados * 100)
                            / tamaño);

                        onProgreso(
                            porcentaje);
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
            esperandoRutaGuardado = false;
            if (hebraArchivo != null && hebraArchivo.IsAlive)
            {
                hebraArchivo.Join(1000);
            }
        }

        ~classComunicacion()
        {
            CerrarPuerto();
            DetenerEnvio();
        }
    }
}










