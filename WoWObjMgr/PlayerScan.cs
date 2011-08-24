// WoW Object Manager
// Copyright (C) 2010 John Moore
// 
// Thanks to jbrauman of MMOwned.com for basis of code
// MemoryReader.dll is not an original work of John Moore
// 
// This program is not associated with or endorsed by Blizzard Entertainment in any way. 
// World of Warcraft is copyright of Blizzard Entertainment.
// 
// 
// http://www.programiscellaneous.com/programming-projects/small-miscellaneous/wow-object-manager/
// 
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
// 
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
// 
// You should have received a copy of the GNU General Public License
// along with this program.  If not, see http://www.gnu.org/licenses/

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Collections;

namespace WoWObjMgr
{
    public class PlayerScan
    {
        uint ClientConnection = 0;
        uint ObjectManager = 0;
        uint FirstObject = 0;

        //Offsets for 3.3.5 build 12340
        //Thanks to MMOwned.com users

        public enum ClientOffsets : uint
        {
            StaticClientConnection = 0x00C79CE0,
            ObjectManagerOffset = 0x2ED0,
            FirstObjectOffset = 0xAC,
            LocalGuidOffset = 0xC0,
            NextObjectOffset = 0x3C,
            LocalPlayerGUID = 0xBD07A8,
            LocalTargetGUID = 0x00BD07B0,
        }

        public enum NameOffsets : ulong
        {
            nameStore = 0x00C5D938 + 0x8,
            nameMask = 0x24,
            nameBase = 0x1C,
            nameString = 0x20
        }

        enum ObjectOffsets : uint
        {
            Type = 0x14,
            Pos_X = 0x79C,
            Pos_Y = 0x798,
            Pos_Z = 0x7A0,
            Rot = 0x7A8,
            Guid = 0x30,
            UnitFields = 0x8
        }

        enum UnitOffsets : uint
        {
            Level = 0x36 * 4,
            Health = 0x18 * 4,
            Energy = 0x19 * 4,
            MaxHealth = 0x20 * 4,
            SummonedBy = 0xE * 4,
            MaxEnergy = 0x21 * 4
        }

        WowObject LocalPlayer = new WowObject();
        WowObject LocalTarget = new WowObject();
        WowObject CurrentObject = new WowObject();
        WowObject TempObject = new WowObject();

        MemoryReader.Memory WowReader = new MemoryReader.Memory();

        ArrayList CurrentPlayers = new ArrayList();

        public PlayerScan()
        {
            if (LoadAddresses() != true)
            {
                throw new InvalidOperationException("WoW could not be read.");
            }
        }

        public void Ping()
        {
            CurrentPlayers.Clear();

            CurrentObject.BaseAddress = FirstObject;

            LocalPlayer.BaseAddress = GetObjectBaseByGuid(LocalPlayer.Guid);
            LocalPlayer.XPos = WowReader.ReadFloat((IntPtr)(LocalPlayer.BaseAddress + ObjectOffsets.Pos_X));
            LocalPlayer.YPos = WowReader.ReadFloat((IntPtr)(LocalPlayer.BaseAddress + ObjectOffsets.Pos_Y));
            LocalPlayer.ZPos = WowReader.ReadFloat((IntPtr)(LocalPlayer.BaseAddress + ObjectOffsets.Pos_Z));
            LocalPlayer.Rotation = WowReader.ReadFloat((IntPtr)(LocalPlayer.BaseAddress + ObjectOffsets.Rot));
            LocalPlayer.UnitFieldsAddress = WowReader.ReadUInt32((IntPtr)(LocalPlayer.BaseAddress + ObjectOffsets.UnitFields));
            LocalPlayer.CurrentHealth = WowReader.ReadUInt32((IntPtr)(LocalPlayer.UnitFieldsAddress + UnitOffsets.Health));
            LocalPlayer.CurrentEnergy = WowReader.ReadUInt32((IntPtr)(LocalPlayer.UnitFieldsAddress + UnitOffsets.Energy));
            LocalPlayer.MaxHealth = WowReader.ReadUInt32((IntPtr)(LocalPlayer.UnitFieldsAddress + UnitOffsets.MaxHealth));
            LocalPlayer.Level = WowReader.ReadUInt32((IntPtr)(LocalPlayer.UnitFieldsAddress + UnitOffsets.Level));
            LocalPlayer.MaxEnergy = WowReader.ReadUInt32((IntPtr)(LocalPlayer.UnitFieldsAddress + UnitOffsets.MaxEnergy));
            LocalPlayer.Name = PlayerNameFromGuid(LocalPlayer.Guid);
            if (LocalPlayer.CurrentHealth <= 0) { LocalPlayer.isDead = true; }

            LocalTarget.Guid = WowReader.ReadUInt64((IntPtr)(ClientOffsets.LocalTargetGUID));

            if (LocalTarget.Guid != 0)
            {
                LocalTarget.BaseAddress = GetObjectBaseByGuid(LocalTarget.Guid);
                LocalTarget.XPos = WowReader.ReadFloat((IntPtr)(LocalTarget.BaseAddress + ObjectOffsets.Pos_X));
                LocalTarget.YPos = WowReader.ReadFloat((IntPtr)(LocalTarget.BaseAddress + ObjectOffsets.Pos_Y));
                LocalTarget.ZPos = WowReader.ReadFloat((IntPtr)(LocalTarget.BaseAddress + ObjectOffsets.Pos_Z));
                LocalTarget.Type = (short)WowReader.ReadUInt32((IntPtr)(LocalTarget.BaseAddress + ObjectOffsets.Type));
                LocalTarget.Rotation = WowReader.ReadFloat((IntPtr)(LocalTarget.BaseAddress + ObjectOffsets.Rot));
                LocalTarget.UnitFieldsAddress = WowReader.ReadUInt32((IntPtr)(LocalTarget.BaseAddress + ObjectOffsets.UnitFields));
                LocalTarget.CurrentHealth = WowReader.ReadUInt32((IntPtr)(LocalTarget.UnitFieldsAddress + UnitOffsets.Health));
                LocalTarget.CurrentEnergy = WowReader.ReadUInt32((IntPtr)(LocalTarget.UnitFieldsAddress + UnitOffsets.Energy));
                LocalTarget.MaxHealth = WowReader.ReadUInt32((IntPtr)(LocalTarget.UnitFieldsAddress + UnitOffsets.MaxHealth));
                LocalTarget.Level = WowReader.ReadUInt32((IntPtr)(LocalTarget.UnitFieldsAddress + UnitOffsets.Level));
                LocalTarget.SummonedBy = WowReader.ReadUInt64((IntPtr)(LocalTarget.UnitFieldsAddress + UnitOffsets.SummonedBy));
                LocalTarget.MaxEnergy = WowReader.ReadUInt32((IntPtr)(LocalTarget.UnitFieldsAddress + UnitOffsets.MaxEnergy));

                if (LocalTarget.Type == 3) // not a human player
                    LocalTarget.Name = MobNameFromGuid(LocalTarget.Guid);
                if (LocalTarget.Type == 4) // a human player
                    LocalTarget.Name = PlayerNameFromGuid(LocalTarget.Guid);
                if (LocalTarget.CurrentHealth <= 0) { LocalTarget.isDead = true; }
                //we don't add LocalTarget to the ArrayList because he or she will appear again later
            }

            // read the object manager from first object to last.
            while (CurrentObject.BaseAddress != 0 && CurrentObject.BaseAddress % 2 == 0)
            {
                CurrentObject.Type = (short)(WowReader.ReadUInt32((IntPtr)(CurrentObject.BaseAddress + ObjectOffsets.Type)));

                if (CurrentObject.Type == 4)
                {
                    CurrentObject.UnitFieldsAddress = WowReader.ReadUInt32((IntPtr)(CurrentObject.BaseAddress + ObjectOffsets.UnitFields));
                    CurrentObject.CurrentHealth = WowReader.ReadUInt32((IntPtr)(CurrentObject.UnitFieldsAddress + UnitOffsets.Health));
                    CurrentObject.CurrentEnergy = WowReader.ReadUInt32((IntPtr)(LocalPlayer.UnitFieldsAddress + UnitOffsets.Energy));
                    CurrentObject.MaxHealth = WowReader.ReadUInt32((IntPtr)(CurrentObject.UnitFieldsAddress + UnitOffsets.MaxHealth));
                    CurrentObject.XPos = WowReader.ReadFloat((IntPtr)(CurrentObject.BaseAddress + ObjectOffsets.Pos_X));
                    CurrentObject.YPos = WowReader.ReadFloat((IntPtr)(CurrentObject.BaseAddress + ObjectOffsets.Pos_Y));
                    CurrentObject.ZPos = WowReader.ReadFloat((IntPtr)(CurrentObject.BaseAddress + ObjectOffsets.Pos_Z));
                    CurrentObject.Rotation = WowReader.ReadFloat((IntPtr)(CurrentObject.BaseAddress + ObjectOffsets.Rot));
                    CurrentObject.Guid = WowReader.ReadUInt64((IntPtr)(CurrentObject.BaseAddress + ObjectOffsets.Guid));
                    CurrentObject.Level = WowReader.ReadUInt32((IntPtr)(CurrentObject.UnitFieldsAddress + UnitOffsets.Level));
                    CurrentObject.MaxEnergy = WowReader.ReadUInt32((IntPtr)(CurrentObject.UnitFieldsAddress + UnitOffsets.MaxEnergy));
                    CurrentObject.Name = PlayerNameFromGuid(CurrentObject.Guid);
                    // check to see whether this player is dead or not
                    if (CurrentObject.CurrentHealth <= 0)
                    {
                        CurrentObject.isDead = true;
                    }
                    CurrentPlayers.Add((WowObject)CurrentObject.Clone());
                }
                // set the current object as the next object in the object manager
                CurrentObject.BaseAddress = WowReader.ReadUInt32((IntPtr)(CurrentObject.BaseAddress + ClientOffsets.NextObjectOffset));
            }
        }

        public ArrayList GetPlayerList()
        {
            return (ArrayList)CurrentPlayers.Clone();
        }

        public WowObject GetLocalPlayer()
        {
            return (WowObject)LocalPlayer.Clone();
        }

        public WowObject GetLocalTarget()
        {
            return (WowObject)LocalTarget.Clone();
        }

        private Boolean LoadAddresses()
        {
            // set the process that we want to read from to be World of Warcraft
            WowReader.SetProcess("Wow", "Read");

            // fill in our missing addresses and find our way to the base of the
            // first object in the object manager
            ClientConnection = WowReader.ReadUInt32((IntPtr)(ClientOffsets.StaticClientConnection));
            ObjectManager = WowReader.ReadUInt32((IntPtr)(ClientConnection + ClientOffsets.ObjectManagerOffset));
            FirstObject = WowReader.ReadUInt32((IntPtr)(ObjectManager + ClientOffsets.FirstObjectOffset));
            LocalTarget.Guid = WowReader.ReadUInt64((IntPtr)(ClientOffsets.LocalTargetGUID));
            LocalPlayer.Guid = WowReader.ReadUInt64((IntPtr)(ObjectManager + ClientOffsets.LocalGuidOffset));

            // if the local guid is zero it means that something failed.
            if (LocalPlayer.Guid == 0)
                return false;
            else
                return true;
        }

        private string MobNameFromGuid(ulong Guid)
        {
            uint ObjectBase = GetObjectBaseByGuid(Guid);
            return WowReader.ReadString((IntPtr)(WowReader.ReadUInt32((IntPtr)(WowReader.ReadUInt32((IntPtr)(ObjectBase + 0x964)) + 0x05C))));
        }

        public string PlayerNameFromGuid(ulong guid)
        {
            ulong mask, base_, offset, current, shortGUID, testGUID;

            mask = WowReader.ReadUInt32((IntPtr)((ulong)NameOffsets.nameStore + (ulong)NameOffsets.nameMask));
            base_ = WowReader.ReadUInt32((IntPtr)((ulong)NameOffsets.nameStore + (ulong)NameOffsets.nameBase));

            shortGUID = guid & 0xffffffff; 
            offset = 12 * (mask & shortGUID);

            current = WowReader.ReadUInt32((IntPtr)(base_ + offset + 8));
            offset = WowReader.ReadUInt32((IntPtr)(base_ + offset));

            if ((current & 0x1) == 0x1) { return ""; }

            testGUID = WowReader.ReadUInt32((IntPtr)(current));

            while (testGUID != shortGUID)
            {
                current = WowReader.ReadUInt32((IntPtr)(current + offset + 4));

                if ((current & 0x1) == 0x1) { return ""; }
                testGUID = WowReader.ReadUInt32((IntPtr)(current));
            }

            return WowReader.ReadString((IntPtr)(current + NameOffsets.nameString));
        }

        private uint GetObjectBaseByGuid(ulong Guid)
        {
            TempObject.BaseAddress = FirstObject;

            while (TempObject.BaseAddress != 0)
            {
                TempObject.Guid = WowReader.ReadUInt64((IntPtr)(TempObject.BaseAddress + ObjectOffsets.Guid));
                if (TempObject.Guid == Guid)
                    return TempObject.BaseAddress;
                TempObject.BaseAddress = WowReader.ReadUInt32((IntPtr)(TempObject.BaseAddress + ClientOffsets.NextObjectOffset));
            }

            return 0;
        }

        private ulong GetObjectGuidByBase(uint Base)
        {
            return WowReader.ReadUInt64((IntPtr)(Base + ObjectOffsets.Guid));
        }

    }

    public class WowObject : ICloneable
    {
        // general properties
        public ulong Guid = 0;
        public ulong SummonedBy = 0;
        public float XPos = 0;
        public float YPos = 0;
        public float ZPos = 0;
        public float Rotation = 0;
        public uint BaseAddress = 0;
        public uint UnitFieldsAddress = 0;
        public short Type = 0;
        public String Name = "";

        // more specialised properties (player or mob)
        public uint CurrentHealth = 0;
        public uint MaxHealth = 0;
        public uint CurrentEnergy = 0; // mana, rage and energy will all fall under energy.
        public uint MaxEnergy = 0;
        public uint Level = 0;

        public bool isDead = false;

        public WowObject()
        {
        }

        public WowObject(ulong cGuid, ulong cSummonedBy, float cXPos, float cYPos, float cZPos, float cRotation, uint cBaseAddress, uint cUnitFieldsAddress, short cType, String cName, uint cCurrentHealth, uint cMaxHealth, uint cCurrentEnergy, uint cMaxEnergy, uint cLevel, bool cisDead)
        {
            Guid = cGuid;
            SummonedBy = cSummonedBy;
            XPos = cXPos;
            YPos = cYPos;
            ZPos = cZPos;
            Rotation = cRotation;
            BaseAddress = cBaseAddress;
            UnitFieldsAddress = cUnitFieldsAddress;
            Type = cType;
            Name = cName;
            CurrentHealth = cCurrentHealth;
            MaxHealth = cMaxHealth;
            CurrentEnergy = cCurrentEnergy;
            MaxEnergy = cMaxEnergy;
            Level = cLevel;

            isDead = cisDead;
        }

        public object Clone()
        {
            return new WowObject(Guid, SummonedBy, XPos, YPos, ZPos, Rotation, BaseAddress, UnitFieldsAddress, Type, Name, CurrentHealth, MaxHealth, CurrentEnergy, MaxEnergy, Level, isDead);
        }
    }
}
