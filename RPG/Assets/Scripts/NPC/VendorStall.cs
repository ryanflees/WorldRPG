using System.Collections.Generic;
using UnityEngine;

namespace WorldRPG
{
    /// <summary>
    /// A market stall NPCs can visit. Customers walk to a random free point inside
    /// <see cref="customerArea"/> (a rectangle in front of the counter) and face the vendor post;
    /// if a vendor is tending the stall they chat with each other, otherwise the customer just
    /// stands there for a bit. Post and area are child transforms (blue = post, green = area),
    /// created by WorldRPG/NPC/Setup Vendor Stalls and editable in the scene.
    /// Customers are any component (an NPCBrain today; the player could browse too), so the stall
    /// doesn't depend on who is shopping.
    /// </summary>
    public class VendorStall : MonoBehaviour
    {
        public static readonly List<VendorStall> All = new List<VendorStall>();

        [Tooltip("Where the vendor stands, facing the customers.")]
        public Transform vendorPost;

        [Tooltip("Centre of the customer area; its forward points at the counter.")]
        public Transform customerArea;

        [Tooltip("Customer area size in metres: x = along the counter, y = away from it.")]
        public Vector2 areaSize = new Vector2(2.4f, 0.8f);

        [Tooltip("How many customers can browse at once.")]
        public int maxCustomers = 3;

        [Tooltip("Minimum distance between two customers (m).")]
        public float customerSpacing = 0.75f;

        [Tooltip("NPC tending this stall. Leave empty for an unattended stall.")]
        public NPC vendor;

        readonly Dictionary<Component, Vector3> _customers = new Dictionary<Component, Vector3>();
        readonly HashSet<Component> _chatting = new HashSet<Component>();

        /// <summary>True when the vendor is standing at the post (not off wandering or missing).</summary>
        public bool VendorPresent
        {
            get
            {
                if (vendor == null || vendorPost == null) return false;
                Vector3 d = vendor.transform.position - vendorPost.position;
                return d.x * d.x + d.z * d.z < 0.5f * 0.5f;
            }
        }

        /// <summary>The vendor chats while at least one customer is chatting at the stall.</summary>
        public bool HasChattingCustomer => _chatting.Count > 0;

        /// <summary>A chatting customer, for the vendor to face (null if none).</summary>
        public Component AnyChattingCustomer
        {
            get
            {
                foreach (var c in _chatting) return c;
                return null;
            }
        }

        public bool HasRoom => customerArea != null && _customers.Count < maxCustomers;

        void OnEnable() => All.Add(this);
        void OnDisable() => All.Remove(this);

        /// <summary>Reserves a random point in the customer area, away from the other customers.</summary>
        public bool TryClaimSpot(Component customer, out Vector3 position)
        {
            position = default;
            if (!HasRoom) return false;
            for (int attempt = 0; attempt < 12; attempt++)
            {
                Vector3 candidate = customerArea.TransformPoint(new Vector3(
                    Random.Range(-0.5f, 0.5f) * areaSize.x, 0f, Random.Range(-0.5f, 0.5f) * areaSize.y));
                bool crowded = false;
                foreach (var other in _customers.Values)
                {
                    if ((other - candidate).sqrMagnitude < customerSpacing * customerSpacing) { crowded = true; break; }
                }
                if (crowded) continue;
                _customers[customer] = candidate;
                position = candidate;
                return true;
            }
            return false;
        }

        public void ReleaseSpot(Component customer)
        {
            _customers.Remove(customer);
            _chatting.Remove(customer);
        }

        public void SetCustomerChatting(Component customer, bool chatting)
        {
            if (chatting) _chatting.Add(customer);
            else _chatting.Remove(customer);
        }

        /// <summary>Is a world position inside the customer area (e.g. the player browsing)?</summary>
        public bool IsInArea(Vector3 worldPosition)
        {
            if (customerArea == null) return false;
            Vector3 local = customerArea.InverseTransformPoint(worldPosition);
            return Mathf.Abs(local.x) <= areaSize.x * 0.5f && Mathf.Abs(local.z) <= areaSize.y * 0.5f && Mathf.Abs(local.y) < 1f;
        }

        void OnDrawGizmos()
        {
            if (vendorPost != null)
            {
                Gizmos.color = new Color(0.3f, 0.5f, 1f);
                Gizmos.DrawWireSphere(vendorPost.position + Vector3.up * 0.05f, 0.3f);
                Gizmos.DrawRay(vendorPost.position + Vector3.up * 0.05f, vendorPost.forward * 0.6f);
            }
            if (customerArea != null)
            {
                Gizmos.color = new Color(0.3f, 1f, 0.4f);
                Gizmos.matrix = Matrix4x4.TRS(customerArea.position + Vector3.up * 0.05f, customerArea.rotation, Vector3.one);
                Gizmos.DrawWireCube(Vector3.zero, new Vector3(areaSize.x, 0f, areaSize.y));
                Gizmos.DrawRay(Vector3.zero, Vector3.forward * 0.6f);
                Gizmos.matrix = Matrix4x4.identity;
            }
        }
    }
}
