namespace TourmalineVirtualExperience.Video
{
    using FFmpeg.AutoGen;
    using System;
    using System.Runtime.InteropServices;

    public unsafe class VideoEncoder : IDisposable
    {
        private AVCodecContext* mvarCodecContext;
        private SwsContext* mvarSwsContext;
        private AVFrame* mvarFrame;
        private AVPacket* mvarPacket;

        private readonly int mvarWidth;
        private readonly int mvarHeight;

        private long mvarPts = 0;

        public VideoEncoder(int width, int height, int fps = 20)
        {
            mvarWidth = width;
            mvarHeight = height;

            [System.Runtime.InteropServices.DllImport("kernel32.dll")]
            static extern bool SetDllDirectory(string lpPathName);
            string dllPath = @"C:\Users\8106\Documents\Repository\laretics\TourmalineExperience\SourceTourmalineVirtualExperience\runtimes\win-64\native";

            // Fuerza el directorio
            SetDllDirectory(dllPath);

            // Alternativa más fuerte: añadir al PATH del proceso
            Environment.SetEnvironmentVariable("PATH",
                dllPath + ";" + Environment.GetEnvironmentVariable("PATH"),
                EnvironmentVariableTarget.Process);

            // Registro explícito (a veces ayuda)
            ffmpeg.avdevice_register_all();

            // Buscar codec H.264
            var codec = ffmpeg.avcodec_find_encoder(AVCodecID.AV_CODEC_ID_ADPCM_IMA_WAV);
            //var codec = ffmpeg.avcodec_find_encoder(AVCodecID.AV_CODEC_ID_H264);
            if (codec == null)
                throw new Exception("No se encontró el codec H.264");

            mvarCodecContext = ffmpeg.avcodec_alloc_context3(codec);

            mvarCodecContext->width = width;
            mvarCodecContext->height = height;
            mvarCodecContext->time_base = new AVRational { num = 1, den = fps };
            mvarCodecContext->framerate = new AVRational { num = fps, den = 1 };
            mvarCodecContext->pix_fmt = AVPixelFormat.AV_PIX_FMT_YUV420P;
            mvarCodecContext->flags |= ffmpeg.AV_CODEC_FLAG_GLOBAL_HEADER;

            // Abrir el codec
            int ret = ffmpeg.avcodec_open2(mvarCodecContext, codec, null);
            if (ret < 0)
                throw new Exception($"Error al abrir codec: {ret}");

            // Frame YUV420P
            mvarFrame = ffmpeg.av_frame_alloc();
            mvarFrame->format = (int)AVPixelFormat.AV_PIX_FMT_YUV420P;
            mvarFrame->width = width;
            mvarFrame->height = height;
            ffmpeg.av_frame_get_buffer(mvarFrame, 32);

            // Packet de salida
            mvarPacket = ffmpeg.av_packet_alloc();

            // Conversor BGRA → YUV420P
            mvarSwsContext = ffmpeg.sws_getContext(
                width, height, AVPixelFormat.AV_PIX_FMT_BGRA,
                width, height, AVPixelFormat.AV_PIX_FMT_YUV420P,
                (int)SwsFlags.SWS_BILINEAR, null, null, null);

            Console.WriteLine($"[VideoEncoder] Inicializado {width}x{height} @ {fps} fps");
        }

        public byte[] EncodeFrame(byte[] bgraFrame)
        {
            if (bgraFrame == null || bgraFrame.Length == 0)
                return Array.Empty<byte>();

            fixed (byte* srcPtr = bgraFrame)
            {
                byte*[] srcData = { srcPtr, null, null, null };
                int[] srcLinesize = { mvarWidth * 4, 0, 0, 0 };

                ffmpeg.sws_scale(mvarSwsContext, srcData, srcLinesize, 0, mvarHeight,
                                 mvarFrame->data, mvarFrame->linesize);
            }

            mvarFrame->pts = mvarPts++;

            // Enviar frame al encoder
            int ret = ffmpeg.avcodec_send_frame(mvarCodecContext, mvarFrame);
            if (ret < 0)
                return Array.Empty<byte>();

            // Recibir packet codificado
            ret = ffmpeg.avcodec_receive_packet(mvarCodecContext, mvarPacket);
            if (ret < 0)
                return Array.Empty<byte>();

            // Copiar a byte[]
            byte[] result = new byte[mvarPacket->size];
            Marshal.Copy((IntPtr)mvarPacket->data, result, 0, mvarPacket->size);

            ffmpeg.av_packet_unref(mvarPacket);

            return result;
        }

        public void Dispose()
        {
            if (mvarFrame != null)
            {
                var frame = mvarFrame;
                ffmpeg.av_frame_free(&frame);
                mvarFrame = null;
            }

            if (mvarPacket != null)
            {
                var packet = mvarPacket;
                ffmpeg.av_packet_free(&packet);
                mvarPacket = null;
            }

            if (mvarCodecContext != null)
            {
                var ctx = mvarCodecContext;
                ffmpeg.avcodec_free_context(&ctx);
                mvarCodecContext = null;
            }

            if (mvarSwsContext != null)
            {
                ffmpeg.sws_freeContext(mvarSwsContext);
                mvarSwsContext = null;
            }
        }
    }
}
