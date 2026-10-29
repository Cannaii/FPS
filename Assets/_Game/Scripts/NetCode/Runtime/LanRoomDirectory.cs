using System;
using System.Collections.Generic;

namespace AFPS.NetCode.Runtime
{
    public readonly struct LanRoomEntry
    {
        public readonly string Address;
        public readonly LanRoomAdvertisement Advertisement;
        public readonly double LastSeenSeconds;

        public LanRoomEntry(string address, LanRoomAdvertisement advertisement, double lastSeenSeconds)
        {
            Address = address;
            Advertisement = advertisement;
            LastSeenSeconds = lastSeenSeconds;
        }
    }

    /// <summary>有界房间目录；来源IPv4+游戏端口为键，服务器重启可替换原实例。</summary>
    public sealed class LanRoomDirectory
    {
        public const int MaximumRooms = 32;
        // 单调秒；包含边界，最后一次合法响应满5秒即过期。不是对局Tick或RTT。
        public const double ExpirySeconds = 5;
        private readonly List<LanRoomEntry> rooms = new List<LanRoomEntry>();
        private double lastTime;
        public int Count => rooms.Count;

        public bool Observe(string sourceAddress, in LanRoomAdvertisement advertisement, double now)
        {
            if (!Advance(now) || !advertisement.IsValid
                || !RoomCode.TryEncode(sourceAddress, advertisement.Port, out _, out _))
            {
                return false;
            }
            for (int index = 0; index < rooms.Count; index++)
            {
                var room = rooms[index];
                if (room.Address == sourceAddress && room.Advertisement.Port == advertisement.Port)
                {
                    rooms[index] = new LanRoomEntry(sourceAddress, advertisement, now);
                    return true;
                }
            }
            if (rooms.Count >= MaximumRooms)
            {
                return false;
            }
            rooms.Add(new LanRoomEntry(sourceAddress, advertisement, now));
            rooms.Sort((left, right) =>
            {
                int comparison = string.CompareOrdinal(left.Address, right.Address);
                return comparison != 0 ? comparison : left.Advertisement.Port.CompareTo(right.Advertisement.Port);
            });
            return true;
        }

        public bool Advance(double now)
        {
            if (double.IsNaN(now) || double.IsInfinity(now) || now < 0)
            {
                return false;
            }
            if (now < lastTime)
            {
                // 时钟复位时旧时间戳失效，不能把旧房间无限保留。
                rooms.Clear();
            }
            lastTime = now;
            for (int index = rooms.Count - 1; index >= 0; index--)
            {
                if (now - rooms[index].LastSeenSeconds >= ExpirySeconds)
                {
                    rooms.RemoveAt(index);
                }
            }
            return true;
        }

        public void CopyTo(List<LanRoomEntry> destination)
        {
            if (destination == null)
            {
                throw new ArgumentNullException(nameof(destination));
            }
            destination.Clear();
            destination.AddRange(rooms);
        }

        public void Clear()
        {
            rooms.Clear();
            lastTime = 0;
        }
    }
}
