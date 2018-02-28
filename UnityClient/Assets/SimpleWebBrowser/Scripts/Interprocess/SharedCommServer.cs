using System;
using MessageLibrary;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using System.Threading;
using SharedMemory;

public class SharedTextureReader : SharedArray<byte>
{
    public SharedTextureReader(string name) : base(name) {
    }
    public unsafe IntPtr UnsafeDataPointer() {
        return new IntPtr(BufferStartPtr);
    }
}
public class SharedCommServer : SharedMemServer
{
    private object _locable=new object();
    bool _isWrite = false;
    public SharedCommServer(bool write) : base()
    {
        _isWrite = write;
    }

    public void InitComm(int size, string filename)
    {
        base.Init(size, filename);
        WriteStop();
    }

    private bool CheckIfReady()
    {
        byte[] arr = ReadBytes();
        if (arr != null)
        {
            try
            {
                MemoryStream mstr = new MemoryStream(arr);
                BinaryFormatter bf = new BinaryFormatter();
                EventPacket ep = bf.Deserialize(mstr) as EventPacket;

                if (ep.Type == BrowserEventType.StopPacket)
                    return true;
                else
                    return false;
            }
            catch (Exception ex)
            {
                return false;
            }
        }
        return false;
    }


    public EventPacket GetMessage()
    {
        if (_isWrite)
            return null;
        lock (_locable) {
            byte[] arr = ReadBytes();
            if (arr != null) {
                try {
                    MemoryStream mstr = new MemoryStream(arr);
                    BinaryFormatter bf = new BinaryFormatter();
                    EventPacket ep = bf.Deserialize(mstr) as EventPacket;

                    if (ep != null && ep.Type != BrowserEventType.StopPacket) {
                        WriteStop();
                        return ep;
                    }
                    else {
                        return null;
                    }
                }
                catch (Exception ex) {
                    return null;
                }
            }
        }

        return null;
    }//

    private void WriteStop()
    {
        EventPacket e = new EventPacket
        {
            Type = BrowserEventType.StopPacket
        };

        MemoryStream mstr = new MemoryStream();
        BinaryFormatter bf = new BinaryFormatter();
        bf.Serialize(mstr, e);
        byte[] b = mstr.GetBuffer();
        WriteBytes(b);
    }

    private bool WaitWhileReady() {
        for (int i = 0; i < 100; i++) {
            if (CheckIfReady())
                return true;
                Thread.Sleep(1);
        }
        return false;
    }
    public void WriteMessage(EventPacket ep)
    {
        BinaryFormatter bf = new BinaryFormatter();
        using (MemoryStream mstr = new MemoryStream()) {
            bf.Serialize(mstr, ep);
            byte[] b = mstr.GetBuffer();
            lock (_locable) {
                if(WaitWhileReady())
                    WriteBytes(b);
                else {
                    //write operation timeout, pssible broken connection
                }
            }

            
        }

    }
}
