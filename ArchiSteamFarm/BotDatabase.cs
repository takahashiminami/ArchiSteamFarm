﻿/*
    _                _      _  ____   _                           _____
   / \    _ __  ___ | |__  (_)/ ___| | |_  ___   __ _  _ __ ___  |  ___|__ _  _ __  _ __ ___
  / _ \  | '__|/ __|| '_ \ | |\___ \ | __|/ _ \ / _` || '_ ` _ \ | |_  / _` || '__|| '_ ` _ \
 / ___ \ | |  | (__ | | | || | ___) || |_|  __/| (_| || | | | | ||  _|| (_| || |   | | | | | |
/_/   \_\|_|   \___||_| |_||_||____/  \__|\___| \__,_||_| |_| |_||_|   \__,_||_|   |_| |_| |_|

 Copyright 2015-2017 Łukasz "JustArchi" Domeradzki
 Contact: JustArchi@JustArchi.net

 Licensed under the Apache License, Version 2.0 (the "License");
 you may not use this file except in compliance with the License.
 You may obtain a copy of the License at

 http://www.apache.org/licenses/LICENSE-2.0
					
 Unless required by applicable law or agreed to in writing, software
 distributed under the License is distributed on an "AS IS" BASIS,
 WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 See the License for the specific language governing permissions and
 limitations under the License.

*/

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using Newtonsoft.Json;

namespace ArchiSteamFarm {
	internal sealed class BotDatabase {
		[JsonProperty(Required = Required.DisallowNull)]
		private readonly ConcurrentHashSet<ulong> BlacklistedFromTradesSteamIDs = new ConcurrentHashSet<ulong>();

		private readonly object FileLock = new object();

		[JsonProperty(Required = Required.DisallowNull)]
		private readonly ConcurrentHashSet<uint> IdlingPriorityAppIDs = new ConcurrentHashSet<uint>();

		internal string LoginKey {
			get => _LoginKey;

			set {
				if (_LoginKey == value) {
					return;
				}

				_LoginKey = value;
				Save();
			}
		}

		internal MobileAuthenticator MobileAuthenticator {
			get => _MobileAuthenticator;

			set {
				if (_MobileAuthenticator == value) {
					return;
				}

				_MobileAuthenticator = value;
				Save();
			}
		}

		[JsonProperty]
		private string _LoginKey;

		[JsonProperty]
		private MobileAuthenticator _MobileAuthenticator;

		private string FilePath;

		// This constructor is used when creating new database
		private BotDatabase(string filePath) {
			if (string.IsNullOrEmpty(filePath)) {
				throw new ArgumentNullException(nameof(filePath));
			}

			FilePath = filePath;
			Save();
		}

		// This constructor is used only by deserializer
		[SuppressMessage("ReSharper", "UnusedMember.Local")]
		private BotDatabase() { }

		internal void AddBlacklistedFromTradesSteamIDs(HashSet<ulong> steamIDs) {
			if ((steamIDs == null) || (steamIDs.Count == 0)) {
				ASF.ArchiLogger.LogNullError(nameof(steamIDs));
				return;
			}

			if (BlacklistedFromTradesSteamIDs.AddRange(steamIDs)) {
				Save();
			}
		}

		internal void AddIdlingPriorityAppIDs(HashSet<uint> appIDs) {
			if ((appIDs == null) || (appIDs.Count == 0)) {
				ASF.ArchiLogger.LogNullError(nameof(appIDs));
				return;
			}

			if (IdlingPriorityAppIDs.AddRange(appIDs)) {
				Save();
			}
		}

		internal void CorrectMobileAuthenticatorDeviceID(string deviceID) {
			if (string.IsNullOrEmpty(deviceID) || (MobileAuthenticator == null)) {
				ASF.ArchiLogger.LogNullError(nameof(deviceID) + " || " + nameof(MobileAuthenticator));
				return;
			}

			if (MobileAuthenticator.CorrectDeviceID(deviceID)) {
				Save();
			}
		}

		internal IEnumerable<ulong> GetBlacklistedFromTradesSteamIDs() => BlacklistedFromTradesSteamIDs;
		internal IEnumerable<uint> GetIdlingPriorityAppIDs() => IdlingPriorityAppIDs;

		internal bool IsBlacklistedFromTrades(ulong steamID) {
			if (steamID == 0) {
				ASF.ArchiLogger.LogNullError(nameof(steamID));
				return false;
			}

			bool result = BlacklistedFromTradesSteamIDs.Contains(steamID);
			return result;
		}

		internal bool IsPriorityIdling(uint appID) {
			if (appID == 0) {
				ASF.ArchiLogger.LogNullError(nameof(appID));
				return false;
			}

			bool result = IdlingPriorityAppIDs.Contains(appID);
			return result;
		}

		internal static BotDatabase Load(string filePath) {
			if (string.IsNullOrEmpty(filePath)) {
				ASF.ArchiLogger.LogNullError(nameof(filePath));
				return null;
			}

			if (!File.Exists(filePath)) {
				return new BotDatabase(filePath);
			}

			BotDatabase botDatabase;

			try {
				botDatabase = JsonConvert.DeserializeObject<BotDatabase>(File.ReadAllText(filePath));
			} catch (Exception e) {
				ASF.ArchiLogger.LogGenericException(e);
				return null;
			}

			if (botDatabase == null) {
				ASF.ArchiLogger.LogNullError(nameof(botDatabase));
				return null;
			}

			botDatabase.FilePath = filePath;
			return botDatabase;
		}

		internal void RemoveBlacklistedFromTradesSteamIDs(HashSet<ulong> steamIDs) {
			if ((steamIDs == null) || (steamIDs.Count == 0)) {
				ASF.ArchiLogger.LogNullError(nameof(steamIDs));
				return;
			}

			if (BlacklistedFromTradesSteamIDs.RemoveRange(steamIDs)) {
				Save();
			}
		}

		internal void RemoveIdlingPriorityAppIDs(HashSet<uint> appIDs) {
			if ((appIDs == null) || (appIDs.Count == 0)) {
				ASF.ArchiLogger.LogNullError(nameof(appIDs));
				return;
			}

			if (IdlingPriorityAppIDs.RemoveRange(appIDs)) {
				Save();
			}
		}

		private void Save() {
			string json = JsonConvert.SerializeObject(this);
			if (string.IsNullOrEmpty(json)) {
				ASF.ArchiLogger.LogNullError(nameof(json));
				return;
			}

			lock (FileLock) {
				string newFilePath = FilePath + ".new";

				try {
					File.WriteAllText(newFilePath, json);

					if (File.Exists(FilePath)) {
						File.Replace(newFilePath, FilePath, null);
					} else {
						File.Move(newFilePath, FilePath);
					}
				} catch (Exception e) {
					ASF.ArchiLogger.LogGenericException(e);
				}
			}
		}
	}
}