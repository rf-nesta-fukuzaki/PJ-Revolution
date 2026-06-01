using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// ローカル Co-op の4スロット状態（人間⇔NPC）を管理する。
/// </summary>
public sealed class LocalCoopRoster
{
    public event Action<int, bool> SlotControlChanged;

    private readonly LocalCoopPartyMember[] _slots = new LocalCoopPartyMember[LocalCoopSettings.MaxPartySize];
    private GameObject _hostPlayerRoot;

    public static LocalCoopRoster Instance { get; private set; }

    public static LocalCoopRoster CreateOrReplace()
    {
        Instance ??= new LocalCoopRoster();
        return Instance;
    }

    public static void ClearInstance()
    {
        Instance = null;
    }

    public void RegisterHost(GameObject hostRoot, LocalCoopPartyMember member)
    {
        _hostPlayerRoot = hostRoot;
        RegisterSlot(0, member);
    }

    public void RegisterSlot(int slot, LocalCoopPartyMember member)
    {
        if (slot < 0 || slot >= _slots.Length) return;
        _slots[slot] = member;
    }

    /// <summary>全スロットとホスト参照をクリアする（セッション切替時のリセット用）。</summary>
    public void ClearAll()
    {
        for (int i = 0; i < _slots.Length; i++)
            _slots[i] = null;
        _hostPlayerRoot = null;
    }

    public void UnregisterSlot(LocalCoopPartyMember member)
    {
        if (member == null) return;
        for (int i = 0; i < _slots.Length; i++)
        {
            if (_slots[i] == member)
                _slots[i] = null;
        }
    }

    public int FindSlotByClientId(ulong clientId)
    {
        if (clientId == ulong.MaxValue) return -1;
        for (int i = 0; i < _slots.Length; i++)
        {
            var member = _slots[i];
            if (member != null && member.NetworkOwnerClientId == clientId)
                return i;
        }
        return -1;
    }

    public LocalCoopPartyMember GetSlot(int slot)
    {
        if (slot < 0 || slot >= _slots.Length) return null;
        return _slots[slot];
    }

    public int HumanCount
    {
        get
        {
            int count = 0;
            for (int i = 0; i < _slots.Length; i++)
            {
                if (_slots[i] != null && _slots[i].IsHumanControlled)
                    count++;
            }
            return count;
        }
    }

    public int FindFirstNpcSlot()
    {
        for (int i = 0; i < _slots.Length; i++)
        {
            if (_slots[i] != null && !_slots[i].IsHumanControlled)
                return i;
        }
        return -1;
    }

    public int FindSlotForGamepad(Gamepad gamepad)
    {
        if (gamepad == null) return -1;
        for (int i = 0; i < _slots.Length; i++)
        {
            var member = _slots[i];
            if (member != null && member.IsHumanControlled && member.AssignedGamepad == gamepad)
                return i;
        }
        return -1;
    }

    public IReadOnlyList<ExplorerController> CollectHumanExplorers()
    {
        var list = new List<ExplorerController>(LocalCoopSettings.MaxPartySize);
        for (int i = 0; i < _slots.Length; i++)
        {
            var member = _slots[i];
            if (member == null || !member.IsHumanControlled) continue;
            var explorer = member.GetComponent<ExplorerController>();
            if (explorer != null)
                list.Add(explorer);
        }
        return list;
    }

    public void SyncHumanCountSetting()
    {
        LocalCoopSettings.HumanCount = HumanCount;
    }

    /// <summary>後入り: NPC スロットを人間操作に切り替える。</summary>
    public bool TryPromoteNpcToHuman(int slot, Gamepad gamepad, SandboxLocalCoopBootstrap bootstrap)
    {
        var member = GetSlot(slot);
        if (member == null || member.IsHumanControlled) return false;
        if (bootstrap == null) return false;

        Vector3 pos = member.transform.position;
        Quaternion rot = member.transform.rotation;
        string displayName = $"Player {slot + 1}";

        if (slot == 0 && _hostPlayerRoot != null)
        {
            bootstrap.EnableHostAsHuman(_hostPlayerRoot, gamepad, displayName);
            RegisterSlot(0, _hostPlayerRoot.GetComponent<LocalCoopPartyMember>());
        }
        else
        {
            UnityEngine.Object.Destroy(member.gameObject);
            var human = bootstrap.SpawnHumanAt(slot, pos, rot, displayName, gamepad);
            if (human == null) return false;
            RegisterSlot(slot, human.GetComponent<LocalCoopPartyMember>());
        }

        SyncHumanCountSetting();
        SlotControlChanged?.Invoke(slot, true);
        Debug.Log($"[LocalCoop] 後入り: スロット {slot} が人間プレイヤーになりました ({displayName})");
        return true;
    }

    /// <summary>後抜け: 人間スロットを NPC に切り替える。</summary>
    public bool TryDemoteHumanToNpc(int slot, SandboxLocalCoopBootstrap bootstrap)
    {
        var member = GetSlot(slot);
        if (member == null || !member.IsHumanControlled) return false;
        if (bootstrap == null) return false;

        Vector3 pos = member.transform.position;
        Quaternion rot = member.transform.rotation;
        int npcNameIndex = slot;
        string npcName = bootstrap.ResolveNpcName(npcNameIndex);
        Color npcColor = bootstrap.ResolveNpcColor(npcNameIndex);

        if (slot == 0 && _hostPlayerRoot != null && member.gameObject == _hostPlayerRoot)
        {
            bootstrap.EnableHostAsNpc(_hostPlayerRoot, npcName);
            RegisterSlot(0, _hostPlayerRoot.GetComponent<LocalCoopPartyMember>());
        }
        else
        {
            var netObj = member.GetComponent<NetworkObject>();
            if (netObj != null && netObj.IsSpawned && NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer)
                netObj.Despawn(true);

            UnityEngine.Object.Destroy(member.gameObject);

            var npc = bootstrap.SpawnNpcAt(slot, pos, npcName, npcColor);
            RegisterSlot(slot, npc.GetComponent<LocalCoopPartyMember>());
        }

        SyncHumanCountSetting();
        SlotControlChanged?.Invoke(slot, false);
        Debug.Log($"[LocalCoop] 後抜け: スロット {slot} が NPC ({npcName}) になりました");
        return true;
    }
}
