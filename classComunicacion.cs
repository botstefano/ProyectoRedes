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


        private Thread hebraEnvio;
        private Thread procesoRecibirMensaje;
        private string MensajeRecibido;
        private byte[] tramaMensajeEnvio;
        private byte[] tramaEnvioBytes;
        private byte[] tramaRecepcionMensaje;
        private byte[] tramaCabacera;
        private string rutaArchivoEnvio;
        private Thread hebraArchivo;

        public classComunicacion()
        {
            MensajeRecibido = "";
            sPuerto = new SerialPort();

            tramaMensajeEnvio = new byte[0];

            hebraEnvio = null;
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
                sPuerto.ReadBufferSize = 4096;// hasta 4 tramas
                sPuerto.WriteBufferSize = 3072;// hasta 3 tramas
                sPuerto.ReadTimeout = 5000;
                sPuerto.WriteTimeout = 5000;
                sPuerto.Open();

            }
            catch (Exception ex)
            {
                MessageBox.Show("ocurrio error");
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
            catch
            {
            }
        }

        private void RecibiendoMensaje(
    int longitud)
        {
            byte[] datos =
                new byte[longitud];

            int total = 0;

            while (total < longitud)
            {
                total += sPuerto.Read(
                    datos,
                    total,
                    longitud - total);
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

                string nombre =
                    partes[0];

                long tamaño =
                    Convert.ToInt64(
                        partes[1]);

                string ruta =
                    Path.Combine(
                         Environment.GetFolderPath(
                             Environment.SpecialFolder.UserProfile),
                         "Downloads",
                         nombre);

                using (FileStream fs =
                       new FileStream(
                           ruta,
                           FileMode.Create))
                {
                    byte[] buffer =
                        new byte[8192];

                    long recibidos = 0;

                    while (recibidos < tamaño)
                    {
                        int faltan =
                            (int)Math.Min(
                                buffer.Length,
                                tamaño - recibidos);

                        int leer =
                            sPuerto.Read(
                                buffer,
                                0,
                                faltan);

                        if (leer <= 0)
                        {
                            throw new Exception(
                                "No llegaron más datos");
                        }

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
                }

                onLlegomensaje(
                    "[Archivo recibido] " +
                    nombre);
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

        public void enviarMensaje(string m)
        {
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
            rutaArchivoEnvio = ruta;

            hebraArchivo = new Thread(EnviandoArchivo);

            hebraArchivo.Start();
        }

        private void EnviandoArchivo()
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
                    new byte[8192];

                int leidos;
                long enviados = 0;

                while ((leidos =
                        fs.Read(
                            buffer,
                            0,
                            buffer.Length)) > 0)
                {
                    sPuerto.Write(
                        buffer,
                        0,
                        leidos);

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

        protected virtual void onProgreso(int p)
        {
            progreso?.Invoke(p);
        }











    }
}










