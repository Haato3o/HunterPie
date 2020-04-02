﻿using System;
using System.IO;
using System.Linq;
using System.Xml;
using System.Threading;
using HunterPie.Memory;
using HunterPie.Logger;
using HunterPie.Core.LPlayer;

namespace HunterPie.Core {
    public class Player {

        // Private variables
        private Int64 _playerAddress = 0x0;
        private int _level;
        private int _zoneId = -1;
        private byte _weaponId;
        private string _sessionId;

        // Game info
        private readonly int[] PeaceZones = new int[11] { 0, 301, 302, 303, 305, 306, 501, 502, 503, 504, 506 };
        private readonly int[] _HBZones = new int[9] { 301, 302, 303, 305, 306, 501, 502, 503, 506 };

        // Player info
        private Int64 LEVEL_ADDRESS;
        private Int64 EQUIPMENT_ADDRESS;
        private Int64 PlayerStructAddress;
        private Int64 PlayerSelectedPointer;
        private int PlayerSlot;
        public Int64 PlayerAddress {
            get { return _playerAddress; }
            set {
                if (_playerAddress != value) {
                    _playerAddress = value;
                    if (value != 0x0) _onLogin();
                }
            }
        }
        public int Level { // Hunter Rank
            get { return _level; }
            set {
                if (_level != value) {
                    _level = value;
                    _onLevelUp();
                }
            }
        }
        public int MasterRank { get; private set; }
        public string Name { get; private set; }
        public int ZoneID {
            get { return _zoneId; }
            set {
                if (_zoneId != value) {
                    if ((_zoneId == -1 || PeaceZones.Contains(_zoneId)) && !PeaceZones.Contains(value)) _onPeaceZoneLeave();
                    if (_HBZones.Contains(_zoneId) && !_HBZones.Contains(value)) _onVillageLeave();
                    _zoneId = value;
                    _onZoneChange();
                    if (PeaceZones.Contains(value)) _onPeaceZoneEnter();
                    if (_HBZones.Contains(value)) _onVillageEnter();
                }
            }
        }
        public string ZoneName {
            get { return GStrings.GetStageNameByID(ZoneID); }
        }
        public int LastZoneID { get; private set; }
        public byte WeaponID {
            get { return _weaponId; }
            set {
                if (_weaponId != value) {
                    _weaponId = value;
                    _onWeaponChange();
                }
            }
        }
        public string WeaponName {
            get { return GStrings.GetWeaponNameByID(WeaponID); }
        }
        public string SessionID {
            get { return _sessionId; }
            set {
                if (_sessionId != value) {
                    _sessionId = value;
                    GetSteamSession();
                    _onSessionChange();
                }
            }
        }
        public bool InPeaceZone {
            get { return PeaceZones.Contains(this.ZoneID); }
        }
        public bool InHarvestZone {
            get { return _HBZones.Contains(ZoneID); }
        }
        public Int64 SteamSession { get; private set; }
        public Int64 SteamID { get; private set; }
        
        // Party
        public Party PlayerParty = new Party();

        // Harvesting & Activities
        public HarvestBox Harvest = new HarvestBox();
        public Activities Activity = new Activities();

        // Mantles
        public Mantle PrimaryMantle = new Mantle();
        public Mantle SecondaryMantle = new Mantle();

        // Abnormalities
        public Abnormalities Abnormalities = new Abnormalities();

        // Threading
        private ThreadStart ScanPlayerInfoRef;
        private Thread ScanPlayerInfo;

        // Player data that will be used eventually
        private Data.Gear Gear = new Data.Gear();

        ~Player() {
            PlayerParty = null;
            Harvest = null;
            PrimaryMantle = null;
            SecondaryMantle = null;
        }

        // Event handlers
        // Level event handler
        public delegate void PlayerEvents(object source, EventArgs args);
        public event PlayerEvents OnLevelChange;
        public event PlayerEvents OnZoneChange;
        public event PlayerEvents OnWeaponChange;
        public event PlayerEvents OnSessionChange;
        public event PlayerEvents OnCharacterLogin;
        public event PlayerEvents OnPeaceZoneEnter;
        public event PlayerEvents OnVillageEnter;
        public event PlayerEvents OnPeaceZoneLeave;
        public event PlayerEvents OnVillageLeave;

        // Dispatchers
        
        protected virtual void _onLogin() {
            OnCharacterLogin?.Invoke(this, EventArgs.Empty);
        }

        protected virtual void _onLevelUp() {
            OnLevelChange?.Invoke(this, EventArgs.Empty);
        }

        protected virtual void _onZoneChange() {
            OnZoneChange?.Invoke(this, EventArgs.Empty);
        }

        protected virtual void _onWeaponChange() {
            OnWeaponChange?.Invoke(this, EventArgs.Empty);
        }

        protected virtual void _onSessionChange() {
            OnSessionChange?.Invoke(this, EventArgs.Empty);
        }

        protected virtual void _onPeaceZoneEnter() {
            OnPeaceZoneEnter?.Invoke(this, EventArgs.Empty);
        }

        protected virtual void _onVillageEnter() {
            OnVillageEnter?.Invoke(this, EventArgs.Empty);
        }

        protected virtual void _onPeaceZoneLeave() {
            OnPeaceZoneLeave?.Invoke(this, EventArgs.Empty);
        }

        protected virtual void _onVillageLeave() {
            OnVillageLeave?.Invoke(this, EventArgs.Empty);
        }

        public void StartScanning() {
            ScanPlayerInfoRef = new ThreadStart(GetPlayerInfo);
            ScanPlayerInfo = new Thread(ScanPlayerInfoRef) {
                Name = "Scanner_Player"
            };
            Debugger.Warn(GStrings.GetLocalizationByXPath("/Console/String[@ID='MESSAGE_PLAYER_SCANNER_INITIALIZED']"));
            ScanPlayerInfo.Start();
        }

        public void StopScanning() {
            ScanPlayerInfo.Abort();
        }

        private void GetPlayerInfo() {
            while (Scanner.GameIsRunning) {
                if (GetPlayerAddress()) {
                    GetPlayerLevel();
                    GetPlayerMasterRank();
                    GetPlayerName();
                    GetWeaponId();
                    GetPlayerGear();
                    GetFertilizers();
                    GetArgosyData();
                    GetTailraidersData();
                    GetSteamFuel();
                    GetPrimaryMantle();
                    GetSecondaryMantle();
                    GetPrimaryMantleTimers();
                    GetSecondaryMantleTimers();
                    GetParty();
                    GetPlayerAbnormalities();
                }
                GetZoneId();
                GetSessionId();
                GetEquipmentAddress();
                Thread.Sleep(Math.Max(50, UserSettings.PlayerConfig.Overlay.GameScanDelay));
            }
            Thread.Sleep(1000);
            GetPlayerInfo();
        }

        private bool GetPlayerAddress() {
            Int64 AddressValue = Scanner.READ_MULTILEVEL_PTR(Address.BASE + Address.WEAPON_OFFSET, Address.Offsets.WeaponOffsets);
            Int64 pAddress = 0x0;
            Int64 nextPlayer = 0x27E9F0;
            if (AddressValue > 0x0) {
                PlayerSelectedPointer = AddressValue;
                string pName = Scanner.READ_STRING(AddressValue - 0x270, 32);
                int pLevel = Scanner.READ_INT(AddressValue - 0x230);
                // If char name starts with a null char then the game haven't launched yet
                if (pName == "") return false;
                for (int playerSlot = 0; playerSlot < 3; playerSlot++) {
                    pAddress = Scanner.READ_MULTILEVEL_PTR(Address.BASE + Address.LEVEL_OFFSET, Address.Offsets.LevelOffsets) + (nextPlayer * playerSlot);
                    if (Scanner.READ_INT(pAddress) == pLevel && Scanner.READ_STRING(pAddress - 0x40, 32)?.Trim('\x00') == pName && PlayerAddress != pAddress) {
                        LEVEL_ADDRESS = pAddress;
                        GetPlayerLevel();
                        GetPlayerName();
                        PlayerAddress = pAddress;
                        this.PlayerSlot = playerSlot;
                        return true;
                    }
                }
            } else {
                PlayerAddress = 0x0;
                LEVEL_ADDRESS = 0x0;
                return false;
            }
            return true;
        }

        private void GetPlayerLevel() {
            Level = Scanner.READ_INT(LEVEL_ADDRESS);
        }

        private void GetPlayerMasterRank() {
            MasterRank = Scanner.READ_INT(LEVEL_ADDRESS + 0x44);
        }

        private void GetPlayerName() {
            Int64 Address = LEVEL_ADDRESS - 0x40;
            Name = Scanner.READ_STRING(Address, 32);
        }

        private void GetZoneId() {
            Int64 ZoneAddress = Scanner.READ_MULTILEVEL_PTR(Address.BASE + Address.ZONE_OFFSET, Address.Offsets.ZoneOffsets);
            int zoneId = Scanner.READ_INT(ZoneAddress);
            if (zoneId != ZoneID) {
                this.LastZoneID = ZoneID;
                this.ZoneID = zoneId;
            }
        }

        public void ChangeLastZone() {
            this.LastZoneID = ZoneID;
        }

        private void GetWeaponId() {
            Int64 Address = Memory.Address.BASE + Memory.Address.WEAPON_OFFSET;
            Address = Scanner.READ_MULTILEVEL_PTR(Address, Memory.Address.Offsets.WeaponOffsets);
            PlayerStructAddress = Address;
            WeaponID = Scanner.READ_BYTE(Address);
        }

        private void GetPlayerGear() {
            Int64 PlayerGearAddress = LEVEL_ADDRESS + 0x18;
            Gear.Weapon = Scanner.READ_INT(PlayerGearAddress);
            Gear.Helmet = Scanner.READ_INT(PlayerGearAddress + 0x04);
            Gear.Armor = Scanner.READ_INT(PlayerGearAddress + 0x08);
            Gear.Gloves = Scanner.READ_INT(PlayerGearAddress + 0x0C);
            Gear.Greaves = Scanner.READ_INT(PlayerGearAddress + 0x10);
            Gear.Charm = Scanner.READ_INT(PlayerGearAddress + 0x14);
            Gear.Mantle = new int[2] { Scanner.READ_INT(PlayerGearAddress + 0x14), Scanner.READ_INT(PlayerGearAddress + 0x18) };
        }

        private void GetSessionId() {
            Int64 Address = Memory.Address.BASE + Memory.Address.SESSION_OFFSET;
            Address = Scanner.READ_MULTILEVEL_PTR(Address, Memory.Address.Offsets.SessionOffsets);
            SessionID = Scanner.READ_STRING(Address, 12);
        }

        private void GetSteamSession() {
            Int64 SteamSessionAddress = Scanner.READ_MULTILEVEL_PTR(Address.BASE + Address.SESSION_OFFSET, Address.Offsets.SessionOffsets);
            SteamSession = Scanner.READ_LONGLONG(SteamSessionAddress + 0x10);
            SteamID = Scanner.READ_LONGLONG(SteamSessionAddress + 0x1184);
        }

        private void GetEquipmentAddress() {
            Int64 Address = Memory.Address.BASE + Memory.Address.EQUIPMENT_OFFSET;
            Address = Scanner.READ_MULTILEVEL_PTR(Address, Memory.Address.Offsets.EquipmentOffsets);
            if (EQUIPMENT_ADDRESS != Address) Debugger.Debug($"New equipment address found -> 0x{Address:X}");
            EQUIPMENT_ADDRESS = Address;
        }

        private void GetPrimaryMantle() {
            Int64 Address = PlayerStructAddress + 0x34;
            int mantleId = Scanner.READ_INT(Address);
            PrimaryMantle.SetID(mantleId);
        }

        private void GetSecondaryMantle() {
            Int64 Address = PlayerStructAddress + 0x34 + 0x4;
            int mantleId = Scanner.READ_INT(Address);
            SecondaryMantle.SetID(mantleId);
        }

        private void GetPrimaryMantleTimers() {
            Int64 PrimaryMantleTimerFixed = (PrimaryMantle.ID * 4) + Address.timerFixed;
            Int64 PrimaryMantleTimer = (PrimaryMantle.ID * 4) + Address.timerDynamic;
            Int64 PrimaryMantleCdFixed = (PrimaryMantle.ID * 4) + Address.cooldownFixed;
            Int64 PrimaryMantleCdDynamic = (PrimaryMantle.ID * 4) + Address.cooldownDynamic;
            PrimaryMantle.SetCooldown(Scanner.READ_FLOAT(EQUIPMENT_ADDRESS + PrimaryMantleCdDynamic), Scanner.READ_FLOAT(EQUIPMENT_ADDRESS + PrimaryMantleCdFixed));
            PrimaryMantle.SetTimer(Scanner.READ_FLOAT(EQUIPMENT_ADDRESS + PrimaryMantleTimer), Scanner.READ_FLOAT(EQUIPMENT_ADDRESS + PrimaryMantleTimerFixed));
        }

        private void GetSecondaryMantleTimers() {
            Int64 SecondaryMantleTimerFixed = (SecondaryMantle.ID * 4) + Address.timerFixed;
            Int64 SecondaryMantleTimer = (SecondaryMantle.ID * 4) + Address.timerDynamic;
            Int64 SecondaryMantleCdFixed = (SecondaryMantle.ID * 4) + Address.cooldownFixed;
            Int64 SecondaryMantleCdDynamic = (SecondaryMantle.ID * 4) + Address.cooldownDynamic;
            SecondaryMantle.SetCooldown(Scanner.READ_FLOAT(EQUIPMENT_ADDRESS + SecondaryMantleCdDynamic), Scanner.READ_FLOAT(EQUIPMENT_ADDRESS + SecondaryMantleCdFixed));
            SecondaryMantle.SetTimer(Scanner.READ_FLOAT(EQUIPMENT_ADDRESS + SecondaryMantleTimer), Scanner.READ_FLOAT(EQUIPMENT_ADDRESS + SecondaryMantleTimerFixed));
        }

        private void GetParty() {
            Int64 address = Address.BASE + Address.PARTY_OFFSET;
            Int64 PartyContainer = Scanner.READ_MULTILEVEL_PTR(address, Address.Offsets.PartyOffsets) - 0x22B7;
            if (this.InPeaceZone) {
                PlayerParty.LobbySize = Scanner.READ_INT(PartyContainer - 0xA961);
            } else {
                int totalDamage = 0;
                for (int i = 0; i < PlayerParty.MaxSize; i++) totalDamage += GetPartyMemberDamage(i);
                PlayerParty.TotalDamage = totalDamage;
                GetQuestElapsedTime();
                for (int i = 0; i < PlayerParty.MaxSize; i++) {
                    string playerName = GetPartyMemberName(PartyContainer + (i * 0x1C0));
                    byte playerWeapon = playerName == this.Name ? this.WeaponID : Scanner.READ_BYTE(PartyContainer + (i * 0x1C0 + 0x33));
                    int playerDamage = GetPartyMemberDamage(i);
                    float playerDamagePercentage = 0;
                    if (totalDamage != 0) {
                        playerDamagePercentage = playerDamage / (float)totalDamage;
                    }
                    PlayerParty[i].SetPlayerInfo(playerName, playerWeapon, playerDamage, playerDamagePercentage);
                }
            }
            

        }

        private void GetQuestElapsedTime() {
            Int64 TimerAddress = Scanner.READ_MULTILEVEL_PTR(Address.BASE + Address.ABNORMALITY_OFFSET, Address.Offsets.AbnormalityOffsets);
            float Timer = Scanner.READ_FLOAT(TimerAddress + 0xB14);
            PlayerParty.ShowDPS = true;
            if (Timer > 0) {
                PlayerParty.Epoch = TimeSpan.FromSeconds(Timer);
            } else { PlayerParty.Epoch = TimeSpan.Zero; }
        }

        private int GetPartyMemberDamage(int playerIndex) {
            Int64 DPSAddress = Scanner.READ_MULTILEVEL_PTR(Address.BASE + Address.DAMAGE_OFFSET, Address.Offsets.DamageOffsets);
            return Scanner.READ_INT(DPSAddress + (0x2A0 * playerIndex));
        }

        private string GetPartyMemberName(Int64 NameAddress) {
            string PartyMemberName = Scanner.READ_STRING(NameAddress, 32);
            return PartyMemberName ?? PartyMemberName.Trim('\x00');
        }

        private void GetFertilizers() {
            Int64 Address = this.LEVEL_ADDRESS;
            
            for (int fertCount = 0; fertCount < 4; fertCount++) {
                // Calculates memory address
                Int64 FertilizerAddress = Address + Memory.Address.Offsets.FertilizersOffset + (0x10 * fertCount);
                // Read memory
                int FertilizerId = Scanner.READ_INT(FertilizerAddress - 0x4);
                int FertilizerCount = Scanner.READ_INT(FertilizerAddress);
                // update fertilizer data
                Harvest.Box[fertCount].ID = FertilizerId;
                Harvest.Box[fertCount].Amount = FertilizerCount;
            }
            UpdateHarvestBoxCounter(Address + Memory.Address.Offsets.FertilizersOffset + (0x10 * 3));
        }

        private void UpdateHarvestBoxCounter(Int64 LastFertAddress) {
            Int64 Address = LastFertAddress + Memory.Address.Offsets.HarvestBoxOffset;
            int counter = 0;
            for (long iAddress = Address; iAddress < Address + 0x330; iAddress += 0x10) {
                int memValue = Scanner.READ_INT(iAddress);
                if (memValue > 0) {
                    counter++;
                }
            }
            Harvest.Counter = counter;
        }

        private void GetSteamFuel() {
            Int64 NaturalFuelAddress = this.LEVEL_ADDRESS + Address.Offsets.SteamFuelOffset;
            Activity.NaturalFuel = Scanner.READ_INT(NaturalFuelAddress);
            Activity.StoredFuel = Scanner.READ_INT(NaturalFuelAddress + 0x4);
        }

        private void GetArgosyData() {
            Int64 ArgosyDaysAddress = this.LEVEL_ADDRESS + Address.Offsets.ArgosyOffset;
            byte ArgosyDays = Scanner.READ_BYTE(ArgosyDaysAddress);
            bool ArgosyInTown = ArgosyDays < 250;
            if (ArgosyDays >= 250) { ArgosyDays = (byte)(byte.MaxValue - ArgosyDays + 1); }
            Activity.SetArgosyInfo(ArgosyDays, ArgosyInTown);
        }

        private void GetTailraidersData() {
            Int64 TailraidersDaysAddress = this.LEVEL_ADDRESS + Address.Offsets.TailRaidersOffset;
            byte TailraidersQuestsDone = Scanner.READ_BYTE(TailraidersDaysAddress);
            bool isDeployed = TailraidersQuestsDone != 255;
            byte QuestsLeft = !isDeployed ? (byte)0 : (byte)(Activity.TailraidersMaxQuest - TailraidersQuestsDone);
            Activity.SetTailraidersInfo(QuestsLeft, isDeployed);
        }

        private void GetPlayerAbnormalities() {
            Int64 AbnormalityBaseAddress = Scanner.READ_MULTILEVEL_PTR(Address.BASE + Address.ABNORMALITY_OFFSET, Address.Offsets.AbnormalityOffsets);
            GetPlayerHuntingHornAbnormalities(AbnormalityBaseAddress);
            GetPlayerPalicoAbnormalities(AbnormalityBaseAddress);
            GetPlayerMiscAbnormalities(AbnormalityBaseAddress);
        }

        private void GetPlayerHuntingHornAbnormalities(Int64 AbnormalityBaseAddress) {
            // Gets the player abnormalities caused by HH
            foreach (XmlNode HHBuff in AbnormalityData.GetHuntingHornAbnormalities()) {
                int BuffOffset = int.Parse(HHBuff.Attributes["Offset"].Value, System.Globalization.NumberStyles.HexNumber);
                bool IsDebuff = bool.Parse(HHBuff.Attributes["IsDebuff"].Value);
                int ID = int.Parse(HHBuff.Attributes["ID"].Value);
                int Stack = int.Parse(HHBuff.Attributes["Stack"].Value);
                GetAbnormality("HUNTINGHORN", AbnormalityBaseAddress + BuffOffset, ID, $"HH_{ID}", IsDebuff, DoubleBuffStack: Stack, ParentOffset: BuffOffset);
            }
        }

        private void GetPlayerPalicoAbnormalities(Int64 AbnormalityBaseAddress) {
            // Gets the player abnormalities caused by palico's skills
            foreach (XmlNode PalBuff in AbnormalityData.GetPalicoAbnormalities()) {
                int BuffOffset = int.Parse(PalBuff.Attributes["Offset"].Value, System.Globalization.NumberStyles.HexNumber);
                bool IsDebuff = bool.Parse(PalBuff.Attributes["IsDebuff"].Value);
                int ID = int.Parse(PalBuff.Attributes["ID"].Value);
                GetAbnormality("PALICO", AbnormalityBaseAddress + BuffOffset, ID, $"PAL_{ID}", IsDebuff);
            }
        }

        private void GetPlayerMiscAbnormalities(Int64 AbnormalityBaseAddress) {
            // Gets the player abnormalities caused by consumables and blights
            // Blights
            foreach (XmlNode Blight in AbnormalityData.GetBlightAbnormalities()) {
                int BuffOffset = int.Parse(Blight.Attributes["Offset"].Value, System.Globalization.NumberStyles.HexNumber);
                bool IsDebuff = bool.Parse(Blight.Attributes["IsDebuff"].Value);
                int ID = int.Parse(Blight.Attributes["ID"].Value);
                GetAbnormality("DEBUFF", AbnormalityBaseAddress + BuffOffset, ID, $"DE_{ID}", IsDebuff);
            }
            foreach (XmlNode MiscBuff in AbnormalityData.GetMiscAbnormalities()) {
                int BuffOffset = int.Parse(MiscBuff.Attributes["Offset"].Value, System.Globalization.NumberStyles.HexNumber);
                bool IsDebuff = bool.Parse(MiscBuff.Attributes["IsDebuff"].Value);
                int ID = int.Parse(MiscBuff.Attributes["ID"].Value);
                bool HasConditions = bool.Parse(MiscBuff.Attributes["HasConditions"].Value);
                bool IsInfinite = false;
                int ConditionOffset = 0;
                if (HasConditions) {
                    IsInfinite = bool.Parse(MiscBuff.Attributes["IsInfinite"].Value);
                    ConditionOffset = int.Parse(MiscBuff.Attributes["ConditionOffset"].Value);
                }
                GetAbnormality("MISC", AbnormalityBaseAddress + BuffOffset, ID, $"MISC_{ID}", IsDebuff, HasConditions, ConditionOffset, IsInfinite);
            }
        }

        private void GetAbnormality(string Type, Int64 AbnormalityAddress, int AbnormNumber, string AbnormInternalID, bool IsDebuff, bool HasConditions = false, int ConditionOffset = 0, bool IsInfinite = false, int DoubleBuffStack = 0, int ParentOffset = 0) {
            float Duration = Scanner.READ_FLOAT(AbnormalityAddress);
            byte Stack;
            // Palico and misc buffs don't stack
            switch (Type) {
                case "HUNTINGHORN":
                    Stack = Scanner.READ_BYTE(AbnormalityAddress + (0x12C - (0x3 * ((ParentOffset - 0x38)/4))));
                    break;
                case "MISC":
                    if (HasConditions) {
                        Stack = (byte)(Scanner.READ_BYTE(AbnormalityAddress + ConditionOffset));
                        if (Stack == (byte)0) { HasConditions = false; }
                    } else { Stack = 0; }
                    break;
                default:
                    Stack = 0;
                    break;
            }
            if ((int)Duration <= 0 && !HasConditions) {
                // Check if there's an abnormality with that ID
                if (Abnormalities[AbnormInternalID] != null) { Abnormalities.Remove(AbnormInternalID); } 
                else { return; }
            } else {
                if ((int)Duration <= 0 && !IsInfinite) {
                    if (Abnormalities[AbnormInternalID] != null) {
                        Abnormalities.Remove(AbnormInternalID);
                    }
                    return;
                }
                if (Stack < DoubleBuffStack) return;
                // Check for existing abnormalities before making a new one
                if (Abnormalities[AbnormInternalID] != null) { Abnormalities[AbnormInternalID].UpdateAbnormalityInfo(Type, AbnormInternalID, Duration, Stack, AbnormNumber, IsDebuff, IsInfinite, AbnormalityData.GetAbnormalityIconByID(Type, AbnormNumber)); } 
                else {
                    Abnormality NewAbnorm = new Abnormality();
                    NewAbnorm.UpdateAbnormalityInfo(Type, AbnormInternalID, Duration, Stack, AbnormNumber, IsDebuff, IsInfinite, AbnormalityData.GetAbnormalityIconByID(Type, AbnormNumber));
                    Abnormalities.Add(AbnormInternalID, NewAbnorm);
                }
            }
        }
    }
}
