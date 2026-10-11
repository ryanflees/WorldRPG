using UnityEngine;

namespace WorldRPG
{
    /// <summary>
    /// Typed access to BasicAC.controller's layers and states. The state/parameter IDs and Play*
    /// methods are generated (BasicAC.Generated.cs); this half adds the animator binding and
    /// state groupings used by the character drivers.
    /// </summary>
    public partial class BasicAC
    {
        readonly Animator m_Animator;

        /// <summary>Standing idles performed by female / male mocap actors, on <see cref="IdleLayer"/>.</summary>
        public readonly int[] FemaleIdleStates;
        public readonly int[] MaleIdleStates;

        /// <summary>Standing chat takes performed by female / male mocap actors, on <see cref="IdleLayer"/>.</summary>
        public readonly int[] FemaleChatStates;
        public readonly int[] MaleChatStates;

        public BasicAC(Animator animator)
        {
            m_Animator = animator;
            FemaleIdleStates = new[]
            {
                L_1__STANDING___IDLE___0_1___FEMALE_A_ID, L_1__STANDING___IDLE___0_1___FEMALE_B_ID, L_1__STANDING___IDLE___0_1___FEMALE_C_ID,
                L_1__STANDING___IDLE___0_2___FEMALE_A_ID, L_1__STANDING___IDLE___0_2___FEMALE_B_ID, L_1__STANDING___IDLE___0_2___FEMALE_C_ID,
            };
            MaleIdleStates = new[]
            {
                L_1__STANDING___IDLE___0_1___MALE_A_ID, L_1__STANDING___IDLE___0_1___MALE_B_ID, L_1__STANDING___IDLE___0_1___MALE_C_ID,
                L_1__STANDING___IDLE___0_2___MALE_A_ID, L_1__STANDING___IDLE___0_2___MALE_B_ID, L_1__STANDING___IDLE___0_2___MALE_C_ID,
            };
            FemaleChatStates = new[]
            {
                L_1__STANDING___CHAT___0_1___A___FEMALE_A_ID, L_1__STANDING___CHAT___0_1___B___FEMALE_B_ID, L_1__STANDING___CHAT___0_1___B___FEMALE_C_ID,
                L_1__STANDING___CHAT___0_2___A___FEMALE_A_ID, L_1__STANDING___CHAT___0_2___B___FEMALE_B_ID, L_1__STANDING___CHAT___0_2___B___FEMALE_C_ID,
            };
            MaleChatStates = new[]
            {
                L_1__STANDING___CHAT___0_1___A___MALE_A_ID, L_1__STANDING___CHAT___0_1___A___MALE_B_ID, L_1__STANDING___CHAT___0_1___B___MALE_C_ID,
                L_1__STANDING___CHAT___0_2___A___MALE_A_ID, L_1__STANDING___CHAT___0_2___C___MALE_B_ID, L_1__STANDING___CHAT___0_2___C___MALE_C_ID,
            };
        }

        public Animator Animator => m_Animator;
        public int IdleLayer => LAYER_1;

        /// <summary>Cross-fades to a state on the idle layer, by its generated state ID.</summary>
        public void CrossFadeIdleLayer(int stateId, float fixedTransitionDuration)
        {
            m_Animator.CrossFadeInFixedTime(stateId, fixedTransitionDuration, LAYER_1);
        }

        /// <summary>Jumps straight into a state on the idle layer at a normalized time (no blend).</summary>
        public void PlayIdleLayer(int stateId, float normalizedTime)
        {
            m_Animator.Play(stateId, LAYER_1, normalizedTime);
        }
    }
}
