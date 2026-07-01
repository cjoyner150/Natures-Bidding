using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

[CreateAssetMenu(fileName = "New Apply X To Target Players Status Effector", menuName = "Stats/Status Effects/Apply X To Target Players Effect")]
public class ApplyXToTargetPlayersStatusEffectorSO : StatusEffectorSO
{
    public TargetPlayerType Target;
    public List<StatusEffectorSO> Effects;

    public override List<StatusEffect> GetStatusEffects()
    {
        return new() {
            new ApplyXToTargetPlayersStatusEffect(Target, Effects)
        };
    }
}
