// Copyright (c) 2005-2016, Coveo Solutions Inc.

using System;
using System.IO;
using System.Text;
using Ionic.Zlib;

namespace GSAFeedPushConverter.Utilities
{
    public static class Compression
    {
        public static string GetCompressedBinaryData(string p_Content)
        {
            string data;
            using (MemoryStream dataStream = new MemoryStream(Encoding.UTF8.GetBytes(p_Content ?? ""))) {
                using (MemoryStream compressStream = new MemoryStream()) {
                    using (ZlibStream deflateStream = new ZlibStream(compressStream, CompressionMode.Compress, CompressionLevel.Level8)) {
                        dataStream.CopyTo(deflateStream);
                        deflateStream.Close();
                        data = Convert.ToBase64String(compressStream.ToArray());
                    }
                }
            }

            return data;
        }

        public static string GetDecompressedBinaryData(string p_Content)
        {
            string decompressedData;

            using (MemoryStream dataStream = new MemoryStream(Convert.FromBase64String(p_Content))) {
                using (MemoryStream decompressStream = new MemoryStream()) {
                    using (ZlibStream deflateStream = new ZlibStream(decompressStream, CompressionMode.Decompress)) {
                        dataStream.CopyTo(deflateStream);
                        deflateStream.Close();
                        decompressedData = Encoding.UTF8.GetString(decompressStream.ToArray());
                    }
                }
            }

            return decompressedData;
        }
    }
}
