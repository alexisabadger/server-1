﻿/*
 * This file is part of Project Hybrasyl.
 *
 * This program is free software; you can redistribute it and/or modify
 * it under the terms of the Affero General Public License as published by
 * the Free Software Foundation, version 3.
 *
 * This program is distributed in the hope that it will be useful, but
 * without ANY WARRANTY; without even the implied warranty of MERCHANTABILITY
 * or FITNESS FOR A PARTICULAR PURPOSE. See the Affero General Public License
 * for more details.
 *
 * You should have received a copy of the Affero General Public License along
 * with this program. If not, see <http://www.gnu.org/licenses/>.
 *
 * (C) 2020 ERISCO, LLC 
 *
 * For contributors and individual authors please refer to CONTRIBUTORS.MD.
 * 
 */

using Hybrasyl.Enums;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Hybrasyl;

public class ClientState
{
    private const int BufferSize = 65600;

    private readonly object _recvlock = new();

    private readonly object _sendlock = new();
    private ConcurrentQueue<ClientPacket> _receiveBuffer = new();
    private ConcurrentQueue<ServerPacket> _sendBuffer = new();

    public int BytesReceived;
    public bool Recieving;
    public ManualResetEvent SendComplete = new(false);

    public ClientState(Socket incoming)
    {
        WorkSocket = incoming;
        Id = GlobalConnectionManifest.GetNewConnectionId();
        Connected = true;
    }

    public int SendBufferDepth => _sendBuffer.Count;
    public bool Connected { get; set; }

    public long Id { get; }

    public object ReceiveLock
    {
        get
        {
            var frame = new StackFrame(1);
            GameLog.Debug(
                $"Receive lock acquired by: {frame.GetMethod().Name} on thread {Thread.CurrentThread.ManagedThreadId}");
            return _recvlock;
        }
    }

    public object SendLock
    {
        get
        {
            var frame = new StackFrame(1);
            GameLog.Debug(
                $"Send lock acquired by: {frame.GetMethod().Name} on thread {Thread.CurrentThread.ManagedThreadId}");
            return _sendlock;
        }
    }

    public Socket WorkSocket { get; }

    public byte[] Buffer { get; private set; } = new byte[BufferSize];

    public bool SendBufferEmpty => _sendBuffer.IsEmpty;

    public IEnumerable<byte> ReceiveBufferTake(int range)
    {
        lock (ReceiveLock)
        {
            return Buffer.Take(range);
        }
    }

    public IEnumerable<byte> ReceiveBufferPop(int range)
    {
        var ret = Buffer.Take(range);
        var asList = Buffer.ToList();
        asList.RemoveRange(0, range);
        Buffer = new byte[BufferSize];
        Array.ConstrainedCopy(asList.ToArray(), 0, Buffer, 0, asList.ToArray().Length);
        return ret;
    }

    public void SendBufferAdd(ServerPacket packet)
    {
        _sendBuffer.Enqueue(packet);
    }

    public bool SendBufferPeek(out ServerPacket packet) => _sendBuffer.TryPeek(out packet);

    public bool SendBufferTake(out ServerPacket packet) => _sendBuffer.TryDequeue(out packet);

    public void ResetReceive()
    {
        lock (ReceiveLock)
        {
            Buffer = new byte[BufferSize];
            _receiveBuffer = new ConcurrentQueue<ClientPacket>();
        }
    }

    public void ResetSend()
    {
        _sendBuffer = new ConcurrentQueue<ServerPacket>();
    }

    public void Dispose()
    {
        try
        {
            WorkSocket.Shutdown(SocketShutdown.Both);
            ResetReceive();
            ResetSend();
            WorkSocket.Close();
            WorkSocket.Dispose();
        }
        catch (Exception e)
        {
            Game.ReportException(e);
            WorkSocket.Close();
            WorkSocket.Dispose();
        }

        Connected = false;
    }

    public bool TryGetPacket(out ClientPacket packet)
    {
        packet = null;
        lock (ReceiveLock)
        {
            if (Buffer.Length != 0 && Buffer[0] == 0xAA && Buffer.Length > 3)
            {
                var packetLength = (Buffer[1] << 8) + Buffer[2] + 3;
                // Complete packet, pop it off and return it
                if (BytesReceived >= packetLength)
                {
                    BytesReceived -= packetLength;
                    packet = new ClientPacket(ReceiveBufferPop(packetLength).ToArray());
                    return true;
                }
            }

            return false;
        }
    }

    public void ReceiveBufferAdd(ClientPacket packet)
    {
        _receiveBuffer.Enqueue(packet);
    }

    public bool ReceiveBufferTake(out ClientPacket packet) => _receiveBuffer.TryDequeue(out packet);
}

public class Client
{
    private long _byteHeartbeatReceived;
    private long _byteHeartbeatSent;
    private int _clientTickCount;

    private int _heartbeatA;
    private int _heartbeatB;
    private long _idle;

    private long _lastReceived;
    private long _lastSent = 0;

    private int _localTickCount;  // Make this int32 because it's what the client expects
    private long _tickHeartbeatReceived;
    private long _tickHeartbeatSent;

    public ClientState ClientState;

    public long ConnectedSince;

    public byte ServerOrdinal;

    public Dictionary<byte, ThrottleInfo> ThrottleState = new();

    public Client() { }

    public Client(Socket socket, Server server)
    {
        ClientState = new ClientState(socket);
        Server = server;
        GameLog.InfoFormat("Connection {0} from {1}:{2}", ConnectionId,
            ((IPEndPoint)Socket.RemoteEndPoint).Address.ToString(),
            ((IPEndPoint)Socket.RemoteEndPoint).Port);

        if (server is Lobby)
        {
            EncryptionKey = Game.ActiveConfiguration.ApiEndpoints.EncryptionEndpoint != null
                ? GlobalConnectionManifest.RequestEncryptionKey(Game.ActiveConfiguration.ApiEndpoints.EncryptionEndpoint.Url,
                    ((IPEndPoint)socket.RemoteEndPoint).Address)
                : Encoding.ASCII.GetBytes("UrkcnItnI");
            GameLog.InfoFormat($"EncryptionKey is {Encoding.ASCII.GetString(EncryptionKey)}");

            var valid = Game.ActiveConfiguration.ApiEndpoints.ValidationEndpoint != null
                ? GlobalConnectionManifest.ValidateEncryptionKey(Game.ActiveConfiguration.ApiEndpoints.ValidationEndpoint.Url,
                    new ServerToken
                    { Ip = ((IPEndPoint)socket.RemoteEndPoint).Address.ToString(), Seed = EncryptionKey })
                : true;

            if (!valid)
            {
                GameLog.ErrorFormat("Invalid key from {IP}", ((IPEndPoint)Socket.RemoteEndPoint).Address.ToString());
                socket.Disconnect(true);
            }
        }

        EncryptionKeyTable = new byte[1024];
        _lastReceived = DateTime.Now.Ticks;

        GlobalConnectionManifest.RegisterClient(this);

        ConnectedSince = DateTime.Now.Ticks;
    }

    public bool Connected => ClientState.Connected;

    public Socket Socket => ClientState.WorkSocket;

    private Server Server { get; }

    public long ConnectionId => ClientState.Id;
    //private byte clientOrdinal = 0x00;

    public string RemoteAddress
    {
        get
        {
            if (Socket != null) return ((IPEndPoint)Socket.RemoteEndPoint).Address.ToString();
            return "nil";
        }
    }

    public byte EncryptionSeed { get; set; }
    public byte[] EncryptionKey { get; set; }
    private byte[] EncryptionKeyTable { get; set; }

    public string NewCharacterName { get; set; }
    public string NewCharacterSalt { get; set; }
    public string NewCharacterPassword { get; set; }

    public byte CurrentMusicTrack { get; private set; }

    /// <summary>
    ///     Return the ServerType of a connection, corresponding with Hybrasyl.Utility.ServerTypes
    /// </summary>
    public int ServerType
    {
        get
        {
            if (Server is Lobby) return ServerTypes.Lobby;
            if (Server is Login) return ServerTypes.Login;
            return ServerTypes.World;
        }
    }

    /// <summary>
    ///     Atomically update the byte-based heartbeat values for the 0x3B packet and then
    ///     queue for transmission to the client. This transmission is aborted if the client hasn't
    ///     been alive more than BYTE_HEARTBEAT_INTERVAL seconds.
    ///     If we don't receive a response to the 0x3B heartbeat within REAP_HEARTBEAT_INTERVAL
    ///     the client is automatically disconnected.
    /// </summary>
    public void SendByteHeartbeat()
    {
        var aliveSince = new TimeSpan(DateTime.Now.Ticks - ConnectedSince);
        if (aliveSince.TotalSeconds < Constants.BYTE_HEARTBEAT_INTERVAL)
            return;
        var byteHeartbeat = new ServerPacket(0x3b);
        var a = Random.Shared.Next(254);
        var b = Random.Shared.Next(254);
        Interlocked.Exchange(ref _heartbeatA, a);
        Interlocked.Exchange(ref _heartbeatB, b);
        byteHeartbeat.WriteByte((byte)a);
        byteHeartbeat.WriteByte((byte)b);
        Enqueue(byteHeartbeat);
        Interlocked.Exchange(ref _byteHeartbeatSent, DateTime.Now.Ticks);
    }

    /// <summary>
    ///     Check to see if a client is idle
    /// </summary>
    public void CheckIdle()
    {
        var now = DateTime.Now.Ticks;
        var idletime = new TimeSpan(now - _lastReceived);
        if (idletime.TotalSeconds > Constants.IDLE_TIME)
        {
            GameLog.DebugFormat("cid {0}: idle for {1} seconds, marking as idle", ConnectionId, idletime.TotalSeconds);
            ToggleIdle();
            GameLog.DebugFormat("cid {0}: ToggleIdle: {1}", ConnectionId, IsIdle());
        }
        else
        {
            GameLog.DebugFormat("cid {0}: idle for {1} seconds, not idle", ConnectionId, idletime.TotalSeconds);
        }
    }

    /// <summary>
    ///     Atomically update the tick-based (0x68) heartbeat values and transmit
    ///     it to the client.
    /// </summary>
    public void SendTickHeartbeat()
    {
        var aliveSince = new TimeSpan(DateTime.Now.Ticks - ConnectedSince);
        if (aliveSince.TotalSeconds < Constants.BYTE_HEARTBEAT_INTERVAL)
            return;
        var tickHeartbeat = new ServerPacket(0x68);
        // We never really want to deal with negative values
        var tickCount = Environment.TickCount & int.MaxValue;
        Interlocked.Exchange(ref _localTickCount, tickCount);
        tickHeartbeat.WriteInt32(tickCount);
        Enqueue(tickHeartbeat);
        Interlocked.Exchange(ref _tickHeartbeatSent, DateTime.Now.Ticks);
    }

    /// <summary>
    ///     Check whether the provided byte heartbeat values match what was sent to the client.
    /// </summary>
    /// <param name="a">byteA received from client</param>
    /// <param name="b">byteB received from client</param>
    /// <returns></returns>
    public bool IsHeartbeatValid(byte a, byte b)
    {
        if (a == _heartbeatA && b == _heartbeatB)
        {
            Interlocked.Exchange(ref _byteHeartbeatReceived, DateTime.Now.Ticks);
            return true;
        }

        return false;
    }

    /// <summary>
    ///     Check whether the localTickCount for the tick heartbeat matches what was sent to the client, updating last received
    ///     heartbeat ticks.
    /// </summary>
    /// <param name="localTickCount">Local (server) tick count returned from the client</param>
    /// <param name="clientTickCount">Tick count returned from the client</param>
    /// <returns>Whether or not the heartbeat is valid</returns>
    public bool IsHeartbeatValid(int localTickCount, int clientTickCount)
    {
        if (_localTickCount == localTickCount)
        {
            Interlocked.Exchange(ref _clientTickCount, clientTickCount);
            Interlocked.Exchange(ref _tickHeartbeatReceived, DateTime.Now.Ticks);
            return true;
        }

        return false;
    }

    /// <summary>
    ///     Determine whether either heartbeat has "expired" (meaning REAP_HEARTBEAT_INTERVAL has
    ///     passed since we received a heartbeat response).
    /// </summary>
    /// <returns>True or false, indicating expiration.</returns>
    public bool IsHeartbeatExpired()
    {
        // If we have no record of sending a heartbeat, obviously it hasn't expired
        if (_tickHeartbeatSent == 0 && _byteHeartbeatSent == 0)
            return false;

        var tickSpan = new TimeSpan(_tickHeartbeatReceived - _tickHeartbeatSent);
        var byteSpan = new TimeSpan(_byteHeartbeatReceived - _byteHeartbeatSent);

        GameLog.DebugFormat("cid {0}: tick heartbeat elapsed seconds {1}, byte heartbeat elapsed seconds {2}",
            ConnectionId, tickSpan.TotalSeconds, byteSpan.TotalSeconds);

        if (tickSpan.TotalSeconds > Constants.REAP_HEARTBEAT_INTERVAL ||
            byteSpan.TotalSeconds > Constants.REAP_HEARTBEAT_INTERVAL)
        {
            // DON'T FEAR THE REAPER
            GameLog.InfoFormat("cid {0}: heartbeat expired", ConnectionId);
            return true;
        }

        return false;
    }

    /// <summary>
    ///     Atomically update the last time we received a packet (in ticks).
    ///     This also automatically marks the client as not idle.
    /// </summary>
    public void UpdateLastReceived(bool updateIdle = true)
    {
        Interlocked.Exchange(ref _lastReceived, DateTime.Now.Ticks);
        if (updateIdle)
            Interlocked.Exchange(ref _idle, 0);
        GameLog.DebugFormat("cid {0}: lastReceived now {1}", ConnectionId, _lastReceived);
    }

    /// <summary>
    ///     Atomically set whether or not a client is idle.
    /// </summary>
    public void ToggleIdle()
    {
        if (_idle == 0)
        {
            Interlocked.Exchange(ref _idle, 1);
            return;
        }

        Interlocked.Exchange(ref _idle, 0);
    }

    /// <summary>
    ///     Return a boolean indicating whether or not a client is idle.
    /// </summary>
    public bool IsIdle() => _idle == 1;


    public void Disconnect()
    {
        ClientState.Dispose();
        GlobalConnectionManifest.DeregisterClient(this);
    }

    public byte[] GenerateKey(ushort bRand, byte sRand)
    {
        var key = new byte[9];

        for (var i = 0; i < 9; ++i) key[i] = EncryptionKeyTable[(i * (9 * i + sRand * sRand) + bRand) % 1024];

        return key;
    }

    public void FlushSendBuffer()
    {
        var buffer = new MemoryStream();
        var transmitDelay = 0;

        try
        {
            while (!ClientState.SendBufferEmpty)
            {
                if (ClientState.SendBufferPeek(out var precheck))
                {
                    if (buffer.Length > 0 && precheck.TransmitDelay > 0 && transmitDelay == 0)
                        // If we're dealing with a bunch of packets with delays, batch them together.
                        // Otherwise, send them individually.
                        //GameLog.Warning("TransmitDelay occurring");
                        break;
                    // Limit outbound transmissions to 65k bytes at a time
                    if (buffer.Length >= 65535)
                        //GameLog.Warning("Breaking up into chunks");
                        break;
                }

                if (ClientState.SendBufferTake(out var packet))
                {
                    // If no packets, just call the whole thing off
                    if (packet == null) return;

                    if (packet.ShouldEncrypt)
                    {
                        ++ServerOrdinal;
                        packet.Ordinal = ServerOrdinal;
                        packet.GenerateFooter();
                        packet.Encrypt(this);
                    }

                    if (packet.TransmitDelay > 0)
                        transmitDelay = packet.TransmitDelay;
                    // Write packet to our memory stream
                    buffer.Write(packet.ToArray());
                }
            }

            if (buffer.Length == 0) return;

            // Background enqueue a send with our memory stream
            Task.Run(function: async () =>
            {
                var socketbuf = buffer.ToArray();
                try
                {
                    //GameLog.Info($"transmit: {socketbuf.Length} with delay {transmitDelay}");
                    if (transmitDelay > 0)
                        await Task.Delay(transmitDelay);
                    Socket.BeginSend(socketbuf, 0, socketbuf.Length, 0, SendCallback, ClientState);
                }
                catch (ObjectDisposedException)
                {
                    ClientState.Dispose();
                }
            });
        }
        catch (ObjectDisposedException)
        {
            // Socket is gone, peace out
            ClientState.Dispose();
        }
        catch (Exception e)
        {
            Game.ReportException(e);
            GameLog.Error($"HALP: {e}");
        }
    }

    public void FlushReceiveBuffer()
    {
        lock (ClientState.ReceiveLock)
        {
            try
            {
                ClientPacket packet;
                while (ClientState.ReceiveBufferTake(out packet))
                {
                    if (packet.ShouldEncrypt) packet.Decrypt(this);

                    if (packet.Opcode == 0x39 || packet.Opcode == 0x3A)
                        packet.DecryptDialog();
                    try
                    {
                        if (Server is Lobby)
                        {
                            GameLog.DebugFormat("Lobby: 0x{0:X2}", packet.Opcode);
                            var handler = (Server as Lobby).PacketHandlers[packet.Opcode];
                            handler.Invoke(this, packet);
                            GameLog.DebugFormat("Lobby packet done");
                            UpdateLastReceived();
                        }
                        else if (Server is Login)
                        {
                            GameLog.Debug($"Login: 0x{packet.Opcode:X2}");
                            var handler = (Server as Login).PacketHandlers[packet.Opcode];
                            handler.Invoke(this, packet);
                            GameLog.DebugFormat("Login packet done");
                            UpdateLastReceived();
                        }
                        else
                        {
                            UpdateLastReceived(packet.Opcode != 0x45 &&
                                               packet.Opcode != 0x75);
                            GameLog.Debug($"Queuing: 0x{packet.Opcode:X2}");
                            //packet.DumpPacket();
                            // Check for throttling
                            var throttleResult = Server.PacketThrottleCheck(this, packet);
                            if (throttleResult == ThrottleResult.OK || throttleResult == ThrottleResult.ThrottleEnd ||
                                throttleResult == ThrottleResult.SquelchEnd)
                                World.MessageQueue.Add(new HybrasylClientMessage(packet, ConnectionId));
                            else if (packet.Opcode == 0x06)
                                World.ControlMessageQueue.Add(new HybrasylControlMessage(ControlOpcode.TriggerRefresh,
                                    ConnectionId));
                            else
                                GameLog.Warning($"{this.RemoteAddress}: throttled for {packet.Opcode}");
                        }
                    }
                    catch (Exception e)
                    {
                        Game.ReportException(e);
                        GameLog.ErrorFormat("EXCEPTION IN HANDLING: 0x{0:X2}: {1}", packet.Opcode, e);
                    }
                }
            }
            catch (Exception e)
            {
                Game.ReportException(e);
                Console.WriteLine(e);
                throw;
            }
        }
    }

    public void SendCallback(IAsyncResult ar)
    {
        var state = (ClientState)ar.AsyncState;
        Client client;
        GameLog.DebugFormat(
            $"EndSend: SocketConnected: {state.WorkSocket.Connected}, IAsyncResult: Completed: {ar.IsCompleted}, CompletedSynchronously: {ar.CompletedSynchronously}");

        try
        {
            SocketError errorCode;
            var bytesSent = state.WorkSocket.EndSend(ar, out errorCode);
            if (!GlobalConnectionManifest.ConnectedClients.TryGetValue(state.Id, out client))
            {
                GameLog.ErrorFormat("Send: socket should not exist: cid {0}", state.Id);
                state.WorkSocket.Close();
                state.WorkSocket.Dispose();
                return;
            }

            if (bytesSent == 0 || errorCode != SocketError.Success)
            {
                GameLog.ErrorFormat("cid {0}: disconnected");
                client.Disconnect();
                throw new SocketException((int)errorCode);
            }
        }
        catch (SocketException e)
        {
            Game.ReportException(e);
            GameLog.Error($"Error Code: {e.ErrorCode}, {e.Message}");
            state.WorkSocket.Close();
        }
        catch (ObjectDisposedException)
        {
            //client.Disconnect();
            GameLog.Error("ObjectDisposedException");
            state.WorkSocket.Close();
        }

        state.SendComplete.Set();
    }

    public void GenerateKeyTable(string seed)
    {
        var table = Crypto.HashString(seed, "MD5");
        table = Crypto.HashString(table, "MD5");
        for (var i = 0; i < 31; i++) table += Crypto.HashString(table, "MD5");

        EncryptionKeyTable = Encoding.ASCII.GetBytes(table);
    }

    public void Enqueue(ServerPacket packet)
    {
        GameLog.DebugFormat("Enqueueing ServerPacket {0}", packet.Opcode);
        if (!Connected)
        {
            Disconnect();
            throw new ObjectDisposedException($"cid {ConnectionId}");
        }

        ClientState.SendBufferAdd(packet);
    }

    public void Enqueue(ClientPacket packet)
    {
        GameLog.DebugFormat("Enqueueing ClientPacket {0}", packet.Opcode);
        if (!Connected)
        {
            Disconnect();
            throw new ObjectDisposedException($"cid {ConnectionId}");
        }

        ClientState.ReceiveBufferAdd(packet);
        if (!packet.ShouldEncrypt || (packet.ShouldEncrypt && EncryptionKey != null))
            FlushReceiveBuffer();
    }

    public void Redirect(Redirect redirect, bool isLogoff = false, int transmitDelay = 0)
    {
        GameLog.InfoFormat("Processing redirect");
        GlobalConnectionManifest.RegisterRedirect(this, redirect);
        GameLog.InfoFormat("Redirect: cid {0}", ConnectionId);
        GameLog.Info($"Redirect EncryptionKey is {Encoding.ASCII.GetString(redirect.EncryptionKey)}");
        if (isLogoff) GlobalConnectionManifest.DeregisterClient(this);
        redirect.Destination.ExpectedConnections.TryAdd(redirect.Id, redirect);

        var endPoint = Socket.RemoteEndPoint as IPEndPoint;
        byte[] addressBytes;

        if (Game.RedirectTarget != null)
            addressBytes = Game.RedirectTarget.GetAddressBytes();
        else
            addressBytes = IPAddress.IsLoopback(endPoint.Address)
                ? IPAddress.Loopback.GetAddressBytes()
                : Game.IpAddress.GetAddressBytes();

        Array.Reverse(addressBytes);

        var x03 = new ServerPacket(0x03);
        x03.Write(addressBytes);
        x03.WriteUInt16((ushort)redirect.Destination.Port);
        x03.WriteByte((byte)(redirect.EncryptionKey.Length + Encoding.ASCII.GetBytes(redirect.Name).Length + 7));
        x03.WriteByte(redirect.EncryptionSeed);
        x03.WriteByte((byte)redirect.EncryptionKey.Length);
        x03.Write(redirect.EncryptionKey);
        x03.WriteString8(redirect.Name);
        x03.WriteUInt32(redirect.Id);
        x03.TransmitDelay = transmitDelay == 0 ? 250 : transmitDelay;
        Enqueue(x03);
    }

    public void LoginMessage(string message, byte type)
    {
        var x02 = new ServerPacket(0x02);
        x02.WriteByte(type);
        x02.WriteString8(message);
        Enqueue(x02);
    }

    public void SendMessage(string message, byte type)
    {
        var x0A = new ServerPacket(0x0A);
        x0A.WriteByte(type);
        x0A.WriteString16(message);
        Enqueue(x0A);
    }
}