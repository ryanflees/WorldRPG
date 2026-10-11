using Pathfinding.RVO;
using UnityEngine;

namespace WorldRPG
{
    /// <summary>
    /// Makes NPC local avoidance (RVO) see the player. The player isn't steered by RVO; we just feed
    /// it the CharacterController's velocity each frame so villagers predict where the player is going
    /// and step aside instead of walking into him.
    /// </summary>
    [RequireComponent(typeof(CharacterController), typeof(RVOController))]
    public class PlayerAvoidanceAgent : MonoBehaviour
    {
        CharacterController _controller;
        RVOController _rvo;

        void Awake()
        {
            _controller = GetComponent<CharacterController>();
            _rvo = GetComponent<RVOController>();
            _rvo.radius = _controller.radius;
            _rvo.height = _controller.height;
            _rvo.center = _controller.center.y;
            _rvo.priority = 1f;   // villagers give way to the player
        }

        void Update()
        {
            if (_rvo.rvoAgent == null) return;
            Vector3 v = _controller.velocity;
            v.y = 0f;
            _rvo.velocity = v;
        }
    }
}
