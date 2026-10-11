using UnityEngine;

namespace WorldRPG
{
    public enum NPCRole
    {
        Villager,
        FarmWorker,
        Carpenter,
        Porter,
        Shopkeeper,
        Elder,
        MarketVendor,
        Weaver,
        KitchenWorker,
        TeaVendor,
    }

    public enum NPCGender
    {
        Male,
        Female,
    }

    public enum NPCState
    {
        Idle,       // standing around (or tending a stall)
        Walking,
        Chatting,
    }

    /// <summary>
    /// Root component of every NPC: identity plus the current high-level state.
    /// The state is driven by <see cref="NPCBrain"/> (wander / stand / chat / tend a stall).
    /// The capsule collider gives the player something to click on / bump into.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CapsuleCollider))]
    public class NPC : MonoBehaviour
    {
        [Header("Identity")]
        public string displayName = "Villager";
        public NPCRole role = NPCRole.Villager;
        public NPCGender gender = NPCGender.Male;

        [Header("State")]
        [SerializeField] NPCState state = NPCState.Idle;

        public NPCState State => state;
        public NPCLocomotionAnimator Locomotion { get; private set; }

        void Awake()
        {
            Locomotion = GetComponent<NPCLocomotionAnimator>();
        }

        public void SetState(NPCState newState)
        {
            state = newState;
        }

        void Reset()
        {
            var capsule = GetComponent<CapsuleCollider>();
            capsule.height = 1.8f;
            capsule.radius = 0.3f;
            capsule.center = new Vector3(0f, 0.9f, 0f);
        }
    }
}
