﻿using System;
using HunterPie.Core.Jobs;

namespace HunterPie.Core.Events
{
    public class HuntingHornSongCastEventArgs : EventArgs
    {
        /// <summary>
        /// Current casted songs
        /// </summary>
        public int[] SongsIdsQueue { get; }

        /// <summary>
        /// Unordered version of <see cref="SongsIdsQueue"/>
        /// </summary>
        public int[] RawSongsIdsQueue { get; }

        /// <summary>
        /// First index of <see cref="RawSongsIdsQueue"/>
        /// </summary>
        public long SongIdsFirstIndex { get; }

        /// <summary>
        /// Indicates where to start playing the queued songs, since in the game you can choose whether
        /// you want to start from the first, second or third song
        /// </summary>
        public byte PlayStartAt { get; }

        /// <summary>
        /// Last song index played
        /// </summary>
        public long PlayCurrentAt { get; }

        /// <summary>
        /// Last state of <see cref="PlayCurrentAt"/>
        /// </summary>
        public long PlayLastAt { get; }

        /// <summary>
        /// Whether the player is casting the buffs based on their player Action id.
        /// </summary>
        public bool IsCastingBuffs { get; }

        /// <summary>
        /// Whether the player is double casting songs.
        /// </summary>
        public bool IsDoubleCasting { get; }

        /// <summary>
        /// Whether the song cast was interrupted by either a monster hitting the player
        /// </summary>
        public bool IsCastingInterrupted { get; }

        public HuntingHornSongCastEventArgs(HuntingHorn huntingHorn)
        {
            SongsIdsQueue = huntingHorn.SongIdsQueue;
            RawSongsIdsQueue = huntingHorn.RawSongIdsQueue;
            SongIdsFirstIndex = huntingHorn.SongIdFirstIndex;
            PlayStartAt = huntingHorn.PlayStartAt;
            PlayCurrentAt = huntingHorn.PlayCurrentAt;
            PlayLastAt = huntingHorn.PlayLastAt;
            IsCastingBuffs = huntingHorn.IsCastingBuffs;
            IsDoubleCasting = huntingHorn.IsDoubleCastingBuffs;
            IsCastingInterrupted = huntingHorn.IsCastingInterrupted;
        }
    }
}
