using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using NATUPNPLib;

namespace FileTransfer
{
    internal class PortForward
    {
        private UPnPNAT? _uPnPNAT;

        public PortForward(UPnPNAT? uPnPNAT)
        {
            _uPnPNAT = uPnPNAT;
        }

        public PortForward()
        {
            _uPnPNAT = new UPnPNAT();
        }

        public void AddStaticPortMapping(int ExternalPort, int InternalPort, ProtocolType type, string InternalClient, bool enable, string description)
        {
            if (_uPnPNAT != null)
                _uPnPNAT.StaticPortMappingCollection.Add(ExternalPort, type.ToString().ToUpper(), InternalPort, InternalClient, enable, description);
            else
                throw new NotSupportedException("Cannot map port");

        }

        public void RemoveStaticPortMapping(int ExternalPort, ProtocolType type)
        {
            if (_uPnPNAT != null)
                _uPnPNAT.StaticPortMappingCollection.Remove(ExternalPort, type.ToString().ToUpper());
        }

        public enum ProtocolType
        {
            UDP,
            TCP
        }

        public PortMappingInfo[] PortMappingsInfos
        {
            get
            {
                ArrayList portMappings = new ArrayList();
                int count = _uPnPNAT.StaticPortMappingCollection.Count;
                IEnumerator enumerator = _uPnPNAT.StaticPortMappingCollection.GetEnumerator();
                enumerator.Reset();
                for (int i = 0; i < count; i++)
                {
                    IStaticPortMapping mapping = null;
                    try
                    {
                        if (enumerator.MoveNext())
                            mapping = (IStaticPortMapping)enumerator.Current;
                    }
                    catch { }
                    if (mapping != null)
                        portMappings.Add(new PortMappingInfo(mapping.InternalClient, mapping.ExternalPort, mapping.InternalPort, mapping.Protocol == "TCP" ? ProtocolType.TCP : ProtocolType.UDP, mapping.Description));
                }
                PortMappingInfo[] portMappingInfos = new PortMappingInfo[portMappings.Count];
                portMappings.CopyTo(portMappingInfos);
                return portMappingInfos;
            }
        }

        public class PortMappingInfo
        {
            public string InternalIP;
            public int ExternalPort;
            public int InternalPort;
            public ProtocolType type;
            public string Description;
            public PortMappingInfo(string internalIP, int externalPort, int internalPort, ProtocolType type, string description)
            {
                this.InternalIP = internalIP;
                this.ExternalPort = externalPort;
                this.InternalPort = internalPort;
                this.type = type;
                this.Description = description;
            }
        }


    }
}
