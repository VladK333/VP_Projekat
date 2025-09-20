using System;
using System.IO;
using SmartGrid.Common;

namespace SmartGrid.Service
{
    class SampleWriter : IDisposable
    {
        private TextWriter _writer;
        private bool _disposed = false;
        private readonly string _filePath;

        public SampleWriter(string filePath)
        {
            _filePath = filePath ?? throw new ArgumentNullException(nameof(filePath));
        }

        ~SampleWriter()
        {
            Dispose(false);
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    _writer?.Dispose();
                }
                _disposed = true;
            }
        }

        public void WriteSample(SmartGridSample sample)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(SampleWriter));
            if (sample == null) throw new ArgumentNullException(nameof(sample));

            _writer = File.AppendText(_filePath); // otvara stream
            string line = $"{sample.Timestamp},{sample.FFT1},{sample.FFT2},{sample.FFT3},{sample.FFT4},{sample.PowerUsage},{sample.Frequency}";
            _writer.WriteLine(line);
            _writer.Close(); // zatvara stream odmah nakon pisanja
        }
    }
}
