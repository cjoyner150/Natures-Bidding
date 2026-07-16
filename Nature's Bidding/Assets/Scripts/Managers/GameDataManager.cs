using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityUtils;

public class GameDataManager : Singleton<GameDataManager>
{
    [SerializeField] private ItemDatabase itemDatabase;

    protected override void Awake()
    {
        if (HasInstance) Destroy(gameObject);
        else
        {
            base.Awake();
            DontDestroyOnLoad(gameObject);
            itemDatabase.Initialize();
        }
    }

    public StatusEffectorSO GetEffector(string id) => itemDatabase.Get<StatusEffectorSO>(id);

    public List<StatusEffectorSO> GetEffectors(IEnumerable<string> ids) =>
        ids.Select(id => itemDatabase.Get<StatusEffectorSO>(id))
           .Where(e => e != null)
           .ToList();

    public WeaponConfigSO GetWeapon(string id) => itemDatabase.Get<WeaponConfigSO>(id);

    public List<WeaponConfigSO> GetWeapons(IEnumerable<string> ids) =>
        ids.Select(id => itemDatabase.Get<WeaponConfigSO>(id))
            .Where(w => w != null)
            .ToList();

    public MaskVisualSO GetMask(string id) => itemDatabase.Get<MaskVisualSO>(id);

    public List<MaskVisualSO> GetMasks(IEnumerable<string> ids) =>
        ids.Select(id => itemDatabase.Get<MaskVisualSO>(id))
            .Where(w => w != null)
            .ToList();
}