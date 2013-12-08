using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using DCTools.Structures;
using ProtoBuf;

namespace DCTools
{
    // ReSharper disable InconsistentNaming
    public class DCT
    // ReSharper restore InconsistentNaming
    {
        public static Dictionary<int, KeyValuePair<string, byte[]>> StringIds;
        
        public static Dictionary<string, string> Strings;

        public static Dictionary<string, string> Args;

        public static List<KeyValuePair<string, string>> Values;

        public static List<KeyValuePair<Action, string>> Actions
            = new List<KeyValuePair<Action, string>>
                  {
                      new KeyValuePair<Action, string>(Unpack, "Unpack DataCenter"),
                      new KeyValuePair<Action, string>(Test, "Test DataCenter"),
                      new KeyValuePair<Action, string>(Unique, "Get DataCenter unique values"),
                  };

        public static Dictionary<string, Type> DcTypes
            = new Dictionary<string, Type>
                  {
                      {"01 00", typeof (int)},
                      {"02 00", typeof (float)},
                      {"05 00", typeof (bool)},
                      //Other is strings
                  };

        public static List<DcObject> DcObjects;

        static void Main()
        {
            while (true)
            {
                Console.WriteLine("\n--- DC Tools ---\n");

                for (int i = 0; i < Actions.Count; i++)
                    Console.WriteLine("{0}: {1}", i + 1, Actions[i].Value);

                Console.WriteLine("OTHER: Exit");

                Console.Write("\nSelect action => ");

                int val;
              
                try
                {
                    val = int.Parse(Console.ReadLine() ?? "-1");
                }
                // ReSharper disable EmptyGeneralCatchClause
                catch
                // ReSharper restore EmptyGeneralCatchClause
                {
                    break;
                }

                Actions[val - 1].Key();
            }
        }

        public static void Unpack()
        {
            Console.WriteLine("\n--- DC Unpack ---\n");
            Stopwatch stopwatch = Stopwatch.StartNew();

            //Devide DataCenter

            if (!File.Exists("data.dec"))
                CutFileData("dc.dec", "data.dec", 0x00000294, 0x025D0B04);

            if (!File.Exists("structs.dec"))
                CutFileData("dc.dec", "structs.dec", 0x02600508, 0x039F5100);

            if (!File.Exists("strings.dec"))
                CutFileData("dc.dec", "strings.dec", 0x03A0059C, 0x05320444);

            if (!File.Exists("strings_id.dec"))
                CutFileData("dc.dec", "strings_id.dec", 0x058B7910, 0x05A1D05C);

            if (!File.Exists("args.dec"))
                CutFileData("dc.dec", "args.dec", 0x05A1D068, 0x05A382DC);

            //

            #region StringIds

            if (!File.Exists("strings_id.bin"))
            {
                ReadStringIds("strings_id.dec");

                using (FileStream fs = File.Create("strings_id.bin"))
                {
                    Serializer.Serialize(fs, StringIds);
                }
            }
            else
            {
                using (FileStream fs = File.OpenRead("strings_id.bin"))
                {
                    StringIds = Serializer.Deserialize<Dictionary<int, KeyValuePair<string, byte[]>>>(fs);
                }
            }

            Console.WriteLine("Readed {0} string id's...", StringIds.Count);

            #endregion

            #region Strings

            if (!File.Exists("strings.bin"))
            {
                ReadStrings("strings.dec", 0x020000);

                using (FileStream fs = File.Create("strings.bin"))
                {
                    Serializer.Serialize(fs, Strings);
                }
            }
            else
            {
                using (FileStream fs = File.OpenRead("strings.bin"))
                {
                    Strings = Serializer.Deserialize<Dictionary<string, string>>(fs);
                }
            }

            Console.WriteLine("Readed {0} strings...", Strings.Count);

            #endregion

            ReadArgs("args.dec");

            ReadValues("data.dec");

            ReadObjects("structs.dec");

            Console.WriteLine("Readed {0} structures...", DcObjects.Count);

            //

            Console.WriteLine("Build DataCenter protobuf...");

            DataCenter dataCenter = new DataCenter
                                        {
                                            Values = Values,
                                            Objects = DcObjects,
                                            MainObjects = new List<DcObject>(),
                                        };

            bool[] used = new bool[DcObjects.Count];

            for (int i = 0; i < DcObjects.Count; i++)
                for (int j = 0; j < DcObjects[i].SubCount; j++)
                    used[DcObjects[i].SubShift + j] = true;

            for (int i = 0; i < DcObjects.Count; i++)
            {
                if (used[i] || DcObjects[i].Name == "Hash")
                    continue;

                dataCenter.MainObjects.Add(DcObjects[i]);
            }

            using (FileStream fs = File.Create("dc.bin"))
            {
                Serializer.Serialize(fs, dataCenter);
            }

            //

            stopwatch.Stop();
            Console.WriteLine("\rAll done in {0}s", (stopwatch.ElapsedMilliseconds / 1000.0).ToString("0.00"));
        }

        public static void Test()
        {
            Console.WriteLine("\n--- DC Test ---\n");

            DataCenter dc = GetDataCenter();

            var objects = dc.GetMainObjectsByName("SkillData");
            foreach (var dcObject in objects)
            {
                var values = dc.GetValues(dcObject);
                Console.WriteLine(values["huntingZoneId"]);
            }
        }

        public static void Unique()
        {
            Console.WriteLine("\n--- DC Unique ---\n");

            DataCenter dc = GetDataCenter();

            Console.Write("Write object name => ");
            string objectName = Console.ReadLine();

            Console.Write("Write field name => ");
            string fieldName = Console.ReadLine();

            Console.WriteLine();

            List<string> uniqueValues = new List<string>();

            var objects = dc.GetObjectsByName(objectName);

            foreach (var dcObject in objects)
            {
                var values = dc.GetValues(dcObject);

                if (values.ContainsKey(fieldName))
                {
                    if (uniqueValues.Contains(values[fieldName].ToString()))
                        continue;

                    Console.WriteLine(values[fieldName]);
                    uniqueValues.Add(values[fieldName].ToString());
                }
            }

            using (TextWriter w = new StreamWriter(objectName + "_" + fieldName + ".txt"))
            {
                for (int i = 0; i < uniqueValues.Count; i++)
                    w.WriteLine("{0},", uniqueValues[i]);
            }

            Process.Start(objectName + "_" + fieldName + ".txt");
        }

        private static DataCenter _dataCenter;

        public static DataCenter GetDataCenter(string path = "dc.bin")
        {
            if (_dataCenter == null)
            {
                using (FileStream fs = File.OpenRead(path))
                {
                    _dataCenter = Serializer.Deserialize<DataCenter>(fs);
                }
            }

            return _dataCenter;
        }

        static void ReadStringIds(string path)
        {
            StringIds = new Dictionary<int, KeyValuePair<string, byte[]>>();
            int counter = 0;

            using (FileStream fin = File.OpenRead(path))
            {
                while (fin.Position < fin.Length)
                {
                    byte[] data = new byte[4];
                    int readed = fin.Read(data, 0, data.Length);

                    string hex = BitConverter.ToString(data, 0, readed).Replace("-", " ");

                    StringIds.Add(counter++, new KeyValuePair<string, byte[]>(hex, data));
                }
            }
        }

        static void ReadStrings(string path, long del)
        {
            Strings = new Dictionary<string, string>();
            Encoding encoding = Encoding.Unicode;

            long realLength = 0;

            using (FileStream fin = File.OpenRead(path))
            {
                using (BinaryReader r = new BinaryReader(fin))
                {
                    foreach (var val in StringIds.Values)
                    {
                        byte[] shiftData = new byte[4];
                        shiftData[0] = val.Value[2];
                        shiftData[1] = val.Value[3];
                        shiftData[2] = val.Value[0];
                        shiftData[3] = val.Value[1];

                        long shift = BitConverter.ToUInt32(shiftData, 0)*2;

                        shift += (int) (shift/del)*8;

                        fin.Seek(shift, SeekOrigin.Begin);

                        string s = "";
                        short ch;

                        while ((ch = r.ReadInt16()) != 0)
                            s += encoding.GetString(BitConverter.GetBytes(ch));

                        Strings.Add(val.Key, s);

                        if (fin.Position > realLength)
                            realLength = fin.Position;
                    }
                }
            }
        }

        static void ReadArgs(string path)
        {
            Args = new Dictionary<string, string> {{"00 00", "Hash"}};

            Encoding encoding = Encoding.Unicode;

            using (FileStream fin = File.OpenRead(path))
            {
                using (BinaryReader r = new BinaryReader(fin))
                {
                    while (fin.Position < fin.Length)
                    {
                        string s = "";
                        short ch;

                        while ((ch = r.ReadInt16()) != 0)
                            s += encoding.GetString(BitConverter.GetBytes(ch));

                        Args.Add(BitConverter.ToString(BitConverter.GetBytes(Args.Count), 0, 2).Replace("-", " "), s);
                    }
                }
            }

            Console.WriteLine("Readed {0} args...", Args.Count);
        }

        static void ReadValues(string path)
        {
            Values = new List<KeyValuePair<string, string>>();
            Console.WriteLine("Loading values:");

            using (FileStream decStream = File.OpenRead(path))
            {
                byte[] buffer = new byte[0x80000]; //Do not change

                bool argFound = false;
                Type type = null;

                string key = "", typ = "";

                while (decStream.Position < decStream.Length)
                {
                    int readed = decStream.Read(buffer, 0, buffer.Length);

                    for (int i = 0; i < readed; i += 4)
                    {
                        if (argFound)
                        {
                            object val = null;

                            if (type == typeof (int))
                                val = BitConverter.ToInt32(buffer, i);
                            else if (type == typeof (uint))
                                val = BitConverter.ToUInt32(buffer, i);
                            else if (type == typeof (float))
                                val = BitConverter.ToSingle(buffer, i);
                            else if (type == typeof (bool))
                                val = buffer[i] > 0;
                            else
                            {
                                string hex = BitConverter.ToString(buffer, i, 4).Replace("-", " ");

                                if (Strings.ContainsKey(hex))
                                    val = Strings[hex];
                                else
                                    Console.WriteLine("UNKNOWN DATA: {0} {1}", typ, hex);
                            }

                            argFound = false;
                            type = null;

                            Values.Add(new KeyValuePair<string, string>(key, "" + val));
                        }
                        else
                        {
                            string hex = BitConverter.ToString(buffer, i, 4).Replace("-", " ");

                            key = hex.Substring(0, 5);
                            typ = hex.Substring(6);

                            if (DcTypes.ContainsKey(typ))
                                type = DcTypes[typ];

                            key = Args[key];
                            argFound = true;
                        }
                    }

                    decStream.Read(buffer, 0, 8); //Delimetr

                    Console.Write("\r{0}%", (100f*decStream.Position/decStream.Length).ToString("0.00"));
                }

                Console.WriteLine("\rReaded {0} values...", Values.Count);
            }
        }

        static void ReadObjects(string path)
        {
            DcObjects = new List<DcObject>();

            using (FileStream decStream = File.OpenRead(path))
            {
                long del = 0xFFFF0;

                while (decStream.Position < decStream.Length)
                {
                    byte[] data = new byte[16];

                    if (decStream.Position == del)
                    {
                        decStream.Read(data, 0, 8);
                        del += 0x100008;
                    }

                    decStream.Read(data, 0, data.Length);

                    string keyHex = BitConverter.ToString(data, 0, 2).Replace("-", " ");

                    string name = Args[keyHex];
                    int argsCount = BitConverter.ToUInt16(data, 4);
                    int subCount = BitConverter.ToUInt16(data, 6);

                    byte[] shiftData = new byte[4];
                    shiftData[0] = data[10];
                    shiftData[1] = data[11];
                    shiftData[2] = data[8];
                    shiftData[3] = data[9];

                    int argsShift = BitConverter.ToInt32(shiftData, 0);

                    shiftData[0] = data[14];
                    shiftData[1] = data[15];
                    shiftData[2] = data[12];
                    shiftData[3] = data[13];

                    int subShift = BitConverter.ToInt32(shiftData, 0) - 1;

                    DcObject dcObject = new DcObject
                                            {
                                                Name = name,

                                                ArgsCount = argsCount,
                                                ArgsShift = argsShift,

                                                SubCount = subCount,
                                                SubShift = subShift,
                                            };

                    DcObjects.Add(dcObject);
                }
            }
        }

        static void CutFileData(string fromPath, string toPath, long from, long to)
        {
            using (FileStream fin = File.OpenRead(fromPath))
            {
                using (FileStream fout = File.Create(toPath))
                {
                    fin.Seek(from, SeekOrigin.Begin);

                    while (fin.Position < to)
                    {
                        long size = to - fin.Position;

                        if (size > short.MaxValue)
                            size = short.MaxValue;

                        byte[] data = new byte[size];
                        int readed = fin.Read(data, 0, data.Length);

                        if (readed == 0)
                            break;

                        fout.Write(data, 0, readed);
                    }
                }
            }
        }
    }
}
