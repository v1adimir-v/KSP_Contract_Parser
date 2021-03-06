﻿#region license
/*The MIT License (MIT)
Contract Parser - A static class for storing information on loaded contracts

Copyright (c) 2016 DMagic

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in
all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
THE SOFTWARE.
*/
#endregion

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Contracts;
using FinePrint.Contracts;

namespace ContractParser
{
    public static class contractParser
    {
		private static DictionaryValueList<Guid, contractContainer> activeContracts = new DictionaryValueList<Guid, contractContainer>();
		private static DictionaryValueList<Guid, contractContainer> offeredContracts = new DictionaryValueList<Guid, contractContainer>();
		private static DictionaryValueList<Guid, contractContainer> completedContracts = new DictionaryValueList<Guid, contractContainer>();
		private static DictionaryValueList<Guid, contractContainer> failedContracts = new DictionaryValueList<Guid, contractContainer>();
		private static DictionaryValueList<Guid, contractContainer> declinedContracts = new DictionaryValueList<Guid, contractContainer>();

		public static EventVoid onContractsParsed = new EventVoid("onContractsParsed");
		public static EventData<Contract> onContractStateChange = new EventData<Contract>("onContractStateChange");
		public static EventData<Contract, ContractParameter> onParameterAdded = new EventData<Contract, ContractParameter>("onContractParameterAdded");

		private static bool loaded;

		public static IEnumerator loadContracts()
		{
			if (HighLogic.CurrentGame.Mode != Game.Modes.CAREER)
				yield break;

			loaded = false;

			int i = 0;

			//Agency modifiers don't seem to work unless I wait a few frames before loading contracts
			while (i < 5)
			{
				i++;
				yield return null;
			}

			Debug.Log("[Contract Parser] Loading All Contracts...");

			activeContracts.Clear();
			offeredContracts.Clear();
			completedContracts.Clear();
			failedContracts.Clear();
			declinedContracts.Clear();

			for (int j = 0; j < ContractSystem.Instance.Contracts.Count; j++)
			{
				Contract c = ContractSystem.Instance.Contracts[j];
			
				if (c == null)
				{
					Debug.Log("[Contract Parser] Error in loading null contract from master list");
					continue;
				}

				contractContainer cc = new contractContainer(c);

				if (cc.Root == null)
				{
					Debug.Log(string.Format("[Contract Parser] Error while loading contract of type {0}; skipping", c.GetType().Name));
					continue;
				}

				switch (cc.Root.ContractState)
				{
					case Contract.State.Active:
						addActiveContract(cc);
						continue;
					case Contract.State.Offered:
						addOfferedContract(cc);
						continue;
					case Contract.State.Completed:
						addCompletedContract(cc);
						continue;
					case Contract.State.Cancelled:
					case Contract.State.DeadlineExpired:
					case Contract.State.Failed:
						addFailedContract(cc);
						continue;
					default:
						continue;
				}
			}

			for (int j = 0; j < ContractSystem.Instance.ContractsFinished.Count; j++)
			{
				Contract c = ContractSystem.Instance.ContractsFinished[j];
			
				if (c == null)
				{
					Debug.Log("[Contract Parser] Error in loading contract from finished list");
					continue;
				}

				contractContainer cc = new contractContainer(c);

				if (cc.Root == null)
				{
					Debug.Log(string.Format("[Contract Parser] Error while loading finished contract of type {0}; skipping", c.GetType().Name));
					continue;
				}

				switch (cc.Root.ContractState)
				{
					case Contract.State.Active:
						addActiveContract(cc);
						continue;
					case Contract.State.Offered:
						addOfferedContract(cc);
						continue;
					case Contract.State.Completed:
						addCompletedContract(cc);
						continue;
					case Contract.State.Cancelled:
					case Contract.State.DeadlineExpired:
					case Contract.State.Failed:
						addFailedContract(cc);
						continue;
					default:
						continue;
				}
			}

			if (contractReflection.CCLoaded)
				RefreshCCOfferedContracts();

			loaded = true;

			onContractsParsed.Fire();

			Debug.Log("[Contract Parser] Finished Loading All Contracts");
		}

		public static void RefreshCCOfferedContracts()
		{
			if (contractReflection.CCLoaded)
			{
				List<Contract> ccPending = new List<Contract>();

				ccPending = contractReflection.pendingContracts();

				if (ccPending != null)
				{
					int l = ccPending.Count;

					for (int i = 0; i < l; i++)
					{
						Contract c = ccPending[i];

						if (c == null)
							continue;

						if (c.ContractState != Contract.State.Offered)
							continue;

						contractContainer cc = new contractContainer(c);

						if (cc == null)
							continue;

						addOfferedContract(cc);
					}
				}
			}
		}

		public static int ActiveContractCount
		{
			get { return activeContracts.Count; }
		}

		public static contractContainer getActiveContract(Guid id, bool warn = false)
		{
			if (activeContracts.Contains(id))
				return activeContracts[id];
			else if (warn)
				Debug.Log(string.Format("[Contract Parser] No Active Contract Of ID: [{0}] Found", id));

			return null;
		}

		public static contractContainer getActiveContract(int index, bool warn = false)
		{
			if (activeContracts.Count > index)
				return activeContracts.At(index);
			else if (warn)
				Debug.Log(string.Format("[Contract Parser] No Active Contract At Index: [{0}] Found", index));

			return null;
		}

		public static bool addActiveContract(contractContainer c, bool warn = false)
		{
			if (!activeContracts.Contains(c.ID))
			{
				activeContracts.Add(c.ID, c);
				return true;
			}
			else if (warn)
				Debug.Log(string.Format("[Contract Parser] Active Contract List Already Has Contract [{0} ; ID: {1}]", c.Title, c.ID));

			return false;
		}

		public static bool removeActiveContract(contractContainer c, bool warn = false)
		{
			if (activeContracts.Contains(c.ID))
			{
				activeContracts.Remove(c.ID);
				return true;
			}
			else if (warn)
				Debug.Log(string.Format("[Contract Parser] Contract Not Found In Active Contract List [{0} ; ID: {1}]", c.Title, c.ID));

			return false;
		}

		public static int OffereContractCount
		{
			get { return offeredContracts.Count; }
		}

		public static contractContainer getOfferedContract(Guid id, bool warn = false)
		{
			if (offeredContracts.Contains(id))
				return offeredContracts[id];
			else if (warn)
				Debug.Log(string.Format("[Contract Parser] No Offered Contract Of ID: [{0}] Found", id));

			return null;
		}

		public static contractContainer getOfferedContract(int index, bool warn = false)
		{
			if (offeredContracts.Count > index)
				return offeredContracts.At(index);
			else if (warn)
				Debug.Log(string.Format("[Contract Parser] No Offered Contract At Index: [{0}] Found", index));

			return null;
		}

		public static bool addOfferedContract(contractContainer c, bool warn = false)
		{
			if (!offeredContracts.Contains(c.ID))
			{
				offeredContracts.Add(c.ID, c);
				return true;
			}
			else if (warn)
				Debug.Log(string.Format("[Contract Parser] Offered Contract List Already Has Contract [{0} ; ID: {1}]", c.Title, c.ID));

			return false;
		}

		public static bool removeOfferedContract(contractContainer c, bool warn = false)
		{
			if (offeredContracts.Contains(c.ID))
			{
				offeredContracts.Remove(c.ID);
				return true;
			}
			else if (warn)
				Debug.Log(string.Format("[Contract Parser] Contract Not Found In Offered Contract List [{0} ; ID: {1}]", c.Title, c.ID));

			return false;
		}

		public static int CompletedContractCount
		{
			get { return completedContracts.Count; }
		}

		public static contractContainer getCompletedContract(Guid id, bool warn = false)
		{
			if (completedContracts.Contains(id))
				return completedContracts[id];
			else if (warn)
				Debug.Log(string.Format("[Contract Parser] No Completed Contract Of ID: [{0}] Found", id));

			return null;
		}

		public static contractContainer getCompletedContract(int index, bool warn = false)
		{
			if (completedContracts.Count > index)
				return completedContracts.At(index);
			else if (warn)
				Debug.Log(string.Format("[Contract Parser] No Completed Contract At Index: [{0}] Found", index));

			return null;
		}

		public static bool addCompletedContract(contractContainer c, bool warn = false)
		{
			if (!completedContracts.Contains(c.ID))
			{
				completedContracts.Add(c.ID, c);
				return true;
			}
			else if (warn)
				Debug.Log(string.Format("[Contract Parser] Completed Contract List Already Has Contract [{0} ; ID: {1}]", c.Title, c.ID));

			return false;
		}

		public static bool removeCompletedContract(contractContainer c, bool warn = false)
		{
			if (completedContracts.Contains(c.ID))
			{
				completedContracts.Remove(c.ID);
				return true;
			}
			else if (warn)
				Debug.Log(string.Format("[Contract Parser] Contract Not Found In Completed Contract List [{0} ; ID: {1}]", c.Title, c.ID));

			return false;
		}

		public static int FailedContractCount
		{
			get { return failedContracts.Count; }
		}

		public static contractContainer getFailedContract(Guid id, bool warn = false)
		{
			if (failedContracts.Contains(id))
				return failedContracts[id];
			else if (warn)
				Debug.Log(string.Format("[Contract Parser] No Failed Contract Of ID: [{0}] Found", id));

			return null;
		}

		public static contractContainer getFailedContract(int index, bool warn = false)
		{
			if (failedContracts.Count > index)
				return failedContracts.At(index);
			else if (warn)
				Debug.Log(string.Format("[Contract Parser] No Failed Contract At Index: [{0}] Found", index));

			return null;
		}

		public static bool addFailedContract(contractContainer c, bool warn = false)
		{
			if (!failedContracts.Contains(c.ID))
			{
				failedContracts.Add(c.ID, c);
				return true;
			}
			else if (warn)
				Debug.Log(string.Format("[Contract Parser] Failed Contract List Already Has Contract [{0} ; ID: {1}]", c.Title, c.ID));

			return false;
		}

		public static bool removeFailedContract(contractContainer c, bool warn = false)
		{
			if (failedContracts.Contains(c.ID))
			{
				failedContracts.Remove(c.ID);
				return true;
			}
			else if (warn)
				Debug.Log(string.Format("[Contract Parser] Contract Not Found In Failed Contract List [{0} ; ID: {1}]", c.Title, c.ID));

			return false;
		}


		public static List<contractContainer> getActiveContracts
		{
			get { return activeContracts.Values.ToList(); }
		}

		public static List<contractContainer> getOfferedContracts
		{
			get { return offeredContracts.Values.ToList(); }
		}

		public static List<contractContainer> getCompletedContracts
		{
			get { return completedContracts.Values.ToList(); }
		}

		public static List<contractContainer> getFailedContracts
		{
			get { return failedContracts.Values.ToList(); }
		}

		public static List<contractContainer> getDeclinedContracts
		{
			get { return declinedContracts.Values.ToList(); }
		}

		public static bool Loaded
		{
			get { return loaded; }
			internal set { loaded = value; }
		}
    }
}
