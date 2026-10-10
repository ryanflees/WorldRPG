using UnityEngine;

namespace WorldRPG
{
	// Paste the CodeBlockGenerator output for BasicAC.controller over the region below whenever the
	// controller's states/parameters change. Hand-written members live in BasicAC.cs.
	public partial class BasicAC
	{
		#region -Generated Code DO NOT MODIFY-
		// Parameters
		private const string PARAM_SPEEDNORMALIZED = "SpeedNormalized"; // Float
		private readonly int PARAM_SPEEDNORMALIZED_ID = Animator.StringToHash(PARAM_SPEEDNORMALIZED);

		// States & Layer IDs
		// Layer 0 - Base Layer
		private readonly int LAYER_0 = 0;
		private const string L_0__LOCOMOTION = "Locomotion";
		private readonly int L_0__LOCOMOTION_ID = Animator.StringToHash(L_0__LOCOMOTION);

		// Layer 1 - IdleLayer
		private readonly int LAYER_1 = 1;
		private const string L_1__STANDING___CHAT___0_1___A___FEMALE_A = "Standing_Chat_01_A_FemaleA";
		private readonly int L_1__STANDING___CHAT___0_1___A___FEMALE_A_ID = Animator.StringToHash(L_1__STANDING___CHAT___0_1___A___FEMALE_A);
		private const string L_1__STANDING___CHAT___0_1___A___MALE_A = "Standing_Chat_01_A_MaleA";
		private readonly int L_1__STANDING___CHAT___0_1___A___MALE_A_ID = Animator.StringToHash(L_1__STANDING___CHAT___0_1___A___MALE_A);
		private const string L_1__STANDING___CHAT___0_1___A___MALE_B = "Standing_Chat_01_A_MaleB";
		private readonly int L_1__STANDING___CHAT___0_1___A___MALE_B_ID = Animator.StringToHash(L_1__STANDING___CHAT___0_1___A___MALE_B);
		private const string L_1__STANDING___CHAT___0_1___B___FEMALE_B = "Standing_Chat_01_B_FemaleB";
		private readonly int L_1__STANDING___CHAT___0_1___B___FEMALE_B_ID = Animator.StringToHash(L_1__STANDING___CHAT___0_1___B___FEMALE_B);
		private const string L_1__STANDING___CHAT___0_1___B___FEMALE_C = "Standing_Chat_01_B_FemaleC";
		private readonly int L_1__STANDING___CHAT___0_1___B___FEMALE_C_ID = Animator.StringToHash(L_1__STANDING___CHAT___0_1___B___FEMALE_C);
		private const string L_1__STANDING___CHAT___0_1___B___MALE_C = "Standing_Chat_01_B_MaleC";
		private readonly int L_1__STANDING___CHAT___0_1___B___MALE_C_ID = Animator.StringToHash(L_1__STANDING___CHAT___0_1___B___MALE_C);
		private const string L_1__STANDING___CHAT___0_2___A___FEMALE_A = "Standing_Chat_02_A_FemaleA";
		private readonly int L_1__STANDING___CHAT___0_2___A___FEMALE_A_ID = Animator.StringToHash(L_1__STANDING___CHAT___0_2___A___FEMALE_A);
		private const string L_1__STANDING___CHAT___0_2___A___MALE_A = "Standing_Chat_02_A_MaleA";
		private readonly int L_1__STANDING___CHAT___0_2___A___MALE_A_ID = Animator.StringToHash(L_1__STANDING___CHAT___0_2___A___MALE_A);
		private const string L_1__STANDING___CHAT___0_2___B___FEMALE_B = "Standing_Chat_02_B_FemaleB";
		private readonly int L_1__STANDING___CHAT___0_2___B___FEMALE_B_ID = Animator.StringToHash(L_1__STANDING___CHAT___0_2___B___FEMALE_B);
		private const string L_1__STANDING___CHAT___0_2___B___FEMALE_C = "Standing_Chat_02_B_FemaleC";
		private readonly int L_1__STANDING___CHAT___0_2___B___FEMALE_C_ID = Animator.StringToHash(L_1__STANDING___CHAT___0_2___B___FEMALE_C);
		private const string L_1__STANDING___CHAT___0_2___C___MALE_B = "Standing_Chat_02_C_MaleB";
		private readonly int L_1__STANDING___CHAT___0_2___C___MALE_B_ID = Animator.StringToHash(L_1__STANDING___CHAT___0_2___C___MALE_B);
		private const string L_1__STANDING___CHAT___0_2___C___MALE_C = "Standing_Chat_02_C_MaleC";
		private readonly int L_1__STANDING___CHAT___0_2___C___MALE_C_ID = Animator.StringToHash(L_1__STANDING___CHAT___0_2___C___MALE_C);
		private const string L_1__EMPTY = "Empty";
		private readonly int L_1__EMPTY_ID = Animator.StringToHash(L_1__EMPTY);
		private const string L_1__STANDING___IDLE___0_1___FEMALE_A = "Standing_Idle_01_FemaleA";
		private readonly int L_1__STANDING___IDLE___0_1___FEMALE_A_ID = Animator.StringToHash(L_1__STANDING___IDLE___0_1___FEMALE_A);
		private const string L_1__STANDING___IDLE___0_1___FEMALE_B = "Standing_Idle_01_FemaleB";
		private readonly int L_1__STANDING___IDLE___0_1___FEMALE_B_ID = Animator.StringToHash(L_1__STANDING___IDLE___0_1___FEMALE_B);
		private const string L_1__STANDING___IDLE___0_1___FEMALE_C = "Standing_Idle_01_FemaleC";
		private readonly int L_1__STANDING___IDLE___0_1___FEMALE_C_ID = Animator.StringToHash(L_1__STANDING___IDLE___0_1___FEMALE_C);
		private const string L_1__STANDING___IDLE___0_1___MALE_A = "Standing_Idle_01_MaleA";
		private readonly int L_1__STANDING___IDLE___0_1___MALE_A_ID = Animator.StringToHash(L_1__STANDING___IDLE___0_1___MALE_A);
		private const string L_1__STANDING___IDLE___0_1___MALE_B = "Standing_Idle_01_MaleB";
		private readonly int L_1__STANDING___IDLE___0_1___MALE_B_ID = Animator.StringToHash(L_1__STANDING___IDLE___0_1___MALE_B);
		private const string L_1__STANDING___IDLE___0_1___MALE_C = "Standing_Idle_01_MaleC";
		private readonly int L_1__STANDING___IDLE___0_1___MALE_C_ID = Animator.StringToHash(L_1__STANDING___IDLE___0_1___MALE_C);
		private const string L_1__STANDING___IDLE___0_2___FEMALE_A = "Standing_Idle_02_FemaleA";
		private readonly int L_1__STANDING___IDLE___0_2___FEMALE_A_ID = Animator.StringToHash(L_1__STANDING___IDLE___0_2___FEMALE_A);
		private const string L_1__STANDING___IDLE___0_2___FEMALE_B = "Standing_Idle_02_FemaleB";
		private readonly int L_1__STANDING___IDLE___0_2___FEMALE_B_ID = Animator.StringToHash(L_1__STANDING___IDLE___0_2___FEMALE_B);
		private const string L_1__STANDING___IDLE___0_2___FEMALE_C = "Standing_Idle_02_FemaleC";
		private readonly int L_1__STANDING___IDLE___0_2___FEMALE_C_ID = Animator.StringToHash(L_1__STANDING___IDLE___0_2___FEMALE_C);
		private const string L_1__STANDING___IDLE___0_2___MALE_A = "Standing_Idle_02_MaleA";
		private readonly int L_1__STANDING___IDLE___0_2___MALE_A_ID = Animator.StringToHash(L_1__STANDING___IDLE___0_2___MALE_A);
		private const string L_1__STANDING___IDLE___0_2___MALE_B = "Standing_Idle_02_MaleB";
		private readonly int L_1__STANDING___IDLE___0_2___MALE_B_ID = Animator.StringToHash(L_1__STANDING___IDLE___0_2___MALE_B);
		private const string L_1__STANDING___IDLE___0_2___MALE_C = "Standing_Idle_02_MaleC";
		private readonly int L_1__STANDING___IDLE___0_2___MALE_C_ID = Animator.StringToHash(L_1__STANDING___IDLE___0_2___MALE_C);


		// Play Animations Layer 0
		public void PlayLocomotion(float fixedTransitionDuration)
		{
			m_Animator.CrossFadeInFixedTime(L_0__LOCOMOTION_ID, fixedTransitionDuration, 0);
		}
		// Play Animations Layer 1
		public void PlayStanding_Chat_01_A_FemaleA_1(float fixedTransitionDuration)
		{
			m_Animator.CrossFadeInFixedTime(L_1__STANDING___CHAT___0_1___A___FEMALE_A_ID, fixedTransitionDuration, 1);
		}
		public void PlayStanding_Chat_01_A_MaleA_1(float fixedTransitionDuration)
		{
			m_Animator.CrossFadeInFixedTime(L_1__STANDING___CHAT___0_1___A___MALE_A_ID, fixedTransitionDuration, 1);
		}
		public void PlayStanding_Chat_01_A_MaleB_1(float fixedTransitionDuration)
		{
			m_Animator.CrossFadeInFixedTime(L_1__STANDING___CHAT___0_1___A___MALE_B_ID, fixedTransitionDuration, 1);
		}
		public void PlayStanding_Chat_01_B_FemaleB_1(float fixedTransitionDuration)
		{
			m_Animator.CrossFadeInFixedTime(L_1__STANDING___CHAT___0_1___B___FEMALE_B_ID, fixedTransitionDuration, 1);
		}
		public void PlayStanding_Chat_01_B_FemaleC_1(float fixedTransitionDuration)
		{
			m_Animator.CrossFadeInFixedTime(L_1__STANDING___CHAT___0_1___B___FEMALE_C_ID, fixedTransitionDuration, 1);
		}
		public void PlayStanding_Chat_01_B_MaleC_1(float fixedTransitionDuration)
		{
			m_Animator.CrossFadeInFixedTime(L_1__STANDING___CHAT___0_1___B___MALE_C_ID, fixedTransitionDuration, 1);
		}
		public void PlayStanding_Chat_02_A_FemaleA_1(float fixedTransitionDuration)
		{
			m_Animator.CrossFadeInFixedTime(L_1__STANDING___CHAT___0_2___A___FEMALE_A_ID, fixedTransitionDuration, 1);
		}
		public void PlayStanding_Chat_02_A_MaleA_1(float fixedTransitionDuration)
		{
			m_Animator.CrossFadeInFixedTime(L_1__STANDING___CHAT___0_2___A___MALE_A_ID, fixedTransitionDuration, 1);
		}
		public void PlayStanding_Chat_02_B_FemaleB_1(float fixedTransitionDuration)
		{
			m_Animator.CrossFadeInFixedTime(L_1__STANDING___CHAT___0_2___B___FEMALE_B_ID, fixedTransitionDuration, 1);
		}
		public void PlayStanding_Chat_02_B_FemaleC_1(float fixedTransitionDuration)
		{
			m_Animator.CrossFadeInFixedTime(L_1__STANDING___CHAT___0_2___B___FEMALE_C_ID, fixedTransitionDuration, 1);
		}
		public void PlayStanding_Chat_02_C_MaleB_1(float fixedTransitionDuration)
		{
			m_Animator.CrossFadeInFixedTime(L_1__STANDING___CHAT___0_2___C___MALE_B_ID, fixedTransitionDuration, 1);
		}
		public void PlayStanding_Chat_02_C_MaleC_1(float fixedTransitionDuration)
		{
			m_Animator.CrossFadeInFixedTime(L_1__STANDING___CHAT___0_2___C___MALE_C_ID, fixedTransitionDuration, 1);
		}
		public void PlayEmpty_1(float fixedTransitionDuration)
		{
			m_Animator.CrossFadeInFixedTime(L_1__EMPTY_ID, fixedTransitionDuration, 1);
		}
		public void PlayStanding_Idle_01_FemaleA_1(float fixedTransitionDuration)
		{
			m_Animator.CrossFadeInFixedTime(L_1__STANDING___IDLE___0_1___FEMALE_A_ID, fixedTransitionDuration, 1);
		}
		public void PlayStanding_Idle_01_FemaleB_1(float fixedTransitionDuration)
		{
			m_Animator.CrossFadeInFixedTime(L_1__STANDING___IDLE___0_1___FEMALE_B_ID, fixedTransitionDuration, 1);
		}
		public void PlayStanding_Idle_01_FemaleC_1(float fixedTransitionDuration)
		{
			m_Animator.CrossFadeInFixedTime(L_1__STANDING___IDLE___0_1___FEMALE_C_ID, fixedTransitionDuration, 1);
		}
		public void PlayStanding_Idle_01_MaleA_1(float fixedTransitionDuration)
		{
			m_Animator.CrossFadeInFixedTime(L_1__STANDING___IDLE___0_1___MALE_A_ID, fixedTransitionDuration, 1);
		}
		public void PlayStanding_Idle_01_MaleB_1(float fixedTransitionDuration)
		{
			m_Animator.CrossFadeInFixedTime(L_1__STANDING___IDLE___0_1___MALE_B_ID, fixedTransitionDuration, 1);
		}
		public void PlayStanding_Idle_01_MaleC_1(float fixedTransitionDuration)
		{
			m_Animator.CrossFadeInFixedTime(L_1__STANDING___IDLE___0_1___MALE_C_ID, fixedTransitionDuration, 1);
		}
		public void PlayStanding_Idle_02_FemaleA_1(float fixedTransitionDuration)
		{
			m_Animator.CrossFadeInFixedTime(L_1__STANDING___IDLE___0_2___FEMALE_A_ID, fixedTransitionDuration, 1);
		}
		public void PlayStanding_Idle_02_FemaleB_1(float fixedTransitionDuration)
		{
			m_Animator.CrossFadeInFixedTime(L_1__STANDING___IDLE___0_2___FEMALE_B_ID, fixedTransitionDuration, 1);
		}
		public void PlayStanding_Idle_02_FemaleC_1(float fixedTransitionDuration)
		{
			m_Animator.CrossFadeInFixedTime(L_1__STANDING___IDLE___0_2___FEMALE_C_ID, fixedTransitionDuration, 1);
		}
		public void PlayStanding_Idle_02_MaleA_1(float fixedTransitionDuration)
		{
			m_Animator.CrossFadeInFixedTime(L_1__STANDING___IDLE___0_2___MALE_A_ID, fixedTransitionDuration, 1);
		}
		public void PlayStanding_Idle_02_MaleB_1(float fixedTransitionDuration)
		{
			m_Animator.CrossFadeInFixedTime(L_1__STANDING___IDLE___0_2___MALE_B_ID, fixedTransitionDuration, 1);
		}
		public void PlayStanding_Idle_02_MaleC_1(float fixedTransitionDuration)
		{
			m_Animator.CrossFadeInFixedTime(L_1__STANDING___IDLE___0_2___MALE_C_ID, fixedTransitionDuration, 1);
		}
		#endregion
	}
}
