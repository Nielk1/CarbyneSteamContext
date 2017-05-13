using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using CarbyneSteamContext.Models.BVdf;

namespace CarbyneSteamContext.Models
{
    public static class SteamShortcutDataFile
    {
        public static BVPropertyCollection Read(string steamShortcutFilePath)
        {
            using (FileStream stream = File.OpenRead(steamShortcutFilePath))
            using (BinaryReader reader = new BinaryReader(stream))
            {
                return BVdfFile.ReadPropertyArray(reader);
            }
        }

        public static void Write(string steamShortcutFilePath, BVPropertyCollection data)
        {
            using (FileStream stream = File.OpenWrite(steamShortcutFilePath))
            using (BinaryWriter writer = new BinaryWriter(stream))
            {
                BVdfFile.WritePropertyArray(writer, data);
                writer.Write((byte)0x08);
            }
        }
    }

    //http://translate.google.com/translate?hl=en&sl=ru&u=http://forum.csmania.ru/viewtopic.php%3Ff%3D38%26t%3D30773
    //https://habrahabr.ru/post/268921/
    public class SteamAppInfoDataFile
    {
        public byte Version1;
        public UInt16 Type; // 0x4456 ('DV')
        public byte Version2;
        public UInt32 Version3;
        public List<SteamAppInfoDataFileChunk> chunks;

        public SteamAppInfoDataFile(byte Version1, UInt16 Type, byte Version2, UInt32 Version3, List<SteamAppInfoDataFileChunk> chunks)
        {
            this.Version1 = Version1;
            this.Type = Type;
            this.Version2 = Version2;
            this.Version3 = Version3;
            this.chunks = chunks;
        }

        public class SteamAppInfoDataFileChunk
        {
            public UInt32 AppID;
            public UInt32 State;
            public UInt32 LastUpdate;
            public UInt64 AccessToken;
            public byte[] Checksum;
            public UInt32 LastChangeNumber;
            public BVPropertyCollection data;

            public SteamAppInfoDataFileChunk(UInt32 AppID, UInt32 State, UInt32 LastUpdate, UInt64 AccessToken, byte[] Checksum, UInt32 LastChangeNumber, BVPropertyCollection data)
            {
                this.AppID = AppID;
                this.State = State;
                this.LastUpdate = LastUpdate;
                this.AccessToken = AccessToken;
                this.Checksum = Checksum;
                this.LastChangeNumber = LastChangeNumber;
                this.data = data;
            }
        }

        public static SteamAppInfoDataFile Read(string steamShortcutFilePath)
        {
            using (FileStream stream = File.OpenRead(steamShortcutFilePath))
            using (BinaryReader reader = new BinaryReader(stream))
            {
                List<SteamAppInfoDataFileChunk> Chunks = new List<SteamAppInfoDataFileChunk>();
                byte Version1 = reader.ReadByte();
                UInt16 Type = reader.ReadUInt16();
                byte Version2 = reader.ReadByte();
                UInt32 Version3 = reader.ReadUInt32();
                //while(reader.BaseStream.Position < reader.BaseStream.Length)
                for(;;)
                {
                    UInt32 AppID = reader.ReadUInt32();
                    if (AppID == 0) break;
                    UInt32 DataSize = reader.ReadUInt32();
                    long startPos = reader.BaseStream.Position;
                    //Console.WriteLine($"Expected End Position: {(startPos + DataSize):X8}");

                    UInt32 State = reader.ReadUInt32();
                    UInt32 LastUpdate = reader.ReadUInt32();
                    UInt64 AccessToken = reader.ReadUInt64();
                    byte[] Checksum = reader.ReadBytes(20);
                    UInt32 LastChangeNumber = reader.ReadUInt32();

                    BVPropertyCollection Data = BVdfFile.ReadPropertyArray(reader);
                    //long endPos = reader.BaseStream.Position;
                    if(reader.BaseStream.Position != (startPos + DataSize))
                    {
                        Console.WriteLine("appinfo.vdf chunk data size wrong, adjusting stream position");
                        reader.BaseStream.Seek(startPos + DataSize, SeekOrigin.Begin);
                    }
                    //Console.WriteLine($"*Expected End Position: {(startPos + DataSize):X8}");
                    //Console.WriteLine($"End Position: {(endPos):X8}");

                    SteamAppInfoDataFileChunk Chunk = new SteamAppInfoDataFileChunk(AppID, State, LastUpdate, AccessToken, Checksum, LastChangeNumber, Data);
                    Chunks.Add(Chunk);
                }
                return new SteamAppInfoDataFile(Version1, Type, Version2, Version3, Chunks);
            }
        }
    }

    public class SteamPackageInfoDataFile
    {
        public byte Version1;
        public UInt16 Type; // 0x5556 ('UV').
        public byte Version2;
        public UInt32 Version3;
        public List<SteamPackageInfoDataFileChunk> chunks;

        public SteamPackageInfoDataFile(byte Version1, UInt16 Type, byte Version2, UInt32 Version3, List<SteamPackageInfoDataFileChunk> chunks)
        {
            this.Version1 = Version1;
            this.Type = Type;
            this.Version2 = Version2;
            this.Version3 = Version3;
            this.chunks = chunks;
        }

        public class SteamPackageInfoDataFileChunk
        {
            public UInt32 PackageID;
            public byte[] Checksum;
            public UInt32 LastChangeNumber;
            public BVPropertyCollection data;

            public SteamPackageInfoDataFileChunk(UInt32 PackageID, byte[] Checksum, UInt32 LastChangeNumber, BVPropertyCollection data)
            {
                this.PackageID = PackageID;
                this.Checksum = Checksum;
                this.LastChangeNumber = LastChangeNumber;
                this.data = data;
            }
        }

        public static SteamPackageInfoDataFile Read(string steamShortcutFilePath)
        {
            using (FileStream stream = File.OpenRead(steamShortcutFilePath))
            using (BinaryReader reader = new BinaryReader(stream))
            {
                List<SteamPackageInfoDataFileChunk> Chunks = new List<SteamPackageInfoDataFileChunk>();
                byte Version1 = reader.ReadByte();
                UInt16 Type = reader.ReadUInt16();
                byte Version2 = reader.ReadByte();
                UInt32 Version3 = reader.ReadUInt32();
                //while(reader.BaseStream.Position < reader.BaseStream.Length)
                for (;;)
                {
                    UInt32 PackageID = reader.ReadUInt32();
                    if (PackageID == 0xffffffff) break;
                    byte[] Checksum = reader.ReadBytes(20);
                    UInt32 LastChangeNumber = reader.ReadUInt32();

                    BVPropertyCollection Data = BVdfFile.ReadPropertyArray(reader);

                    SteamPackageInfoDataFileChunk Chunk = new SteamPackageInfoDataFileChunk(PackageID, Checksum, LastChangeNumber, Data);
                    Chunks.Add(Chunk);
                }
                return new SteamPackageInfoDataFile(Version1, Type, Version2, Version3, Chunks);
            }
        }
    }

    public static class BVdfFile
    {
        private static BVProperty ReadProperty(byte token, BinaryReader reader)
        {
            //byte token = reader.ReadByte();
            string Key = ReadString(reader);
            //Console.WriteLine("{0:X2} {1}", token, Key);
            BVToken Value;
            switch (token)
            {
                case 0x00:
                    Value = ReadPropertyArray(reader);
                    break;
                case 0x01:
                    Value = ReadString(reader);
                    //Value = BVToken.Make(ReadString(reader));
                    break;
                case 0x02:
                    Value = reader.ReadInt32();
                    //Value = BVToken.Make(reader.ReadInt32());
                    break;
                case 0x03:
                    Value = reader.ReadSingle();
                    //Value = BVToken.Make(reader.ReadSingle());
                    break;
                case 0x04:
                    throw new Exception("PTR");
                    break;
                case 0x05:
                    throw new Exception("WSTRING");
                    break;
                case 0x06:
                    throw new Exception("COLOR");
                    break;
                case 0x07:
                    Value = reader.ReadUInt64();
                    //Value = BVToken.Make(reader.ReadUInt64());
                    break;
                default:
                    throw new Exception(string.Format("Unknown Type Byte {0:X2}", token));
            }
            return new BVProperty(Key, Value);
        }

        private static void WriteProperty(BinaryWriter writer, BVProperty data)
        {
            if (data.Value.GetType() == typeof(BVPropertyCollection))
            {
                writer.Write((byte)0x00);
                WriteString(writer, data.Key);
                WritePropertyArray(writer, (BVPropertyCollection)data.Value);
                writer.Write((byte)0x08);
            }
            else if (data.Value.GetType() == typeof(BVStringToken))
            {
                writer.Write((byte)0x01);
                WriteString(writer, data.Key);
                WriteString(writer, ((BVStringToken)data.Value).Value);
            }
            else if (data.Value.GetType() == typeof(BVInt32Token))
            {
                writer.Write((byte)0x02);
                WriteString(writer, data.Key);
                writer.Write(((BVInt32Token)data.Value).Value);
            }
            else if (data.Value.GetType() == typeof(BVSingleToken))
            {
                writer.Write((byte)0x03);
                WriteString(writer, data.Key);
                writer.Write(((BVSingleToken)data.Value).Value);
            }
            else if (data.Value.GetType() == typeof(BVUInt64Token))
            {
                writer.Write((byte)0x07);
                WriteString(writer, data.Key);
                writer.Write(((BVUInt64Token)data.Value).Value);
            }
            else
            {
                throw new Exception(string.Format("Unknown Type {0}", data.GetType().ToString()));
            }
        }

        public static BVPropertyCollection ReadPropertyArray(BinaryReader reader)
        {
            BVPropertyCollection Values = new BVPropertyCollection();
            byte ReadByte = 0x00;
            while ((ReadByte = reader.ReadByte()) != 0x08)
            {
                //Values.Add(ReadProperty(reader));
                Values.Add(ReadProperty(ReadByte, reader));
            }
            //reader.ReadByte();
            return Values;
        }

        public static void WritePropertyArray(BinaryWriter writer, BVPropertyCollection data)
        {
            data.Properties.ForEach(dr =>
            {
                WriteProperty(writer, dr);
            });
        }

        public static string ReadString(BinaryReader reader)
        {
            List<byte> values = new List<byte>();
            byte tmp;
            while ((tmp = reader.ReadByte()) != (byte)0x00)
            {
                values.Add(tmp);
            }

            return System.Text.Encoding.UTF8.GetString(values.ToArray());
        }

        public static void WriteString(BinaryWriter writer, string data)
        {
            writer.Write(System.Text.Encoding.UTF8.GetBytes(data));
            writer.Write((byte)0x00);
        }
    }
}
