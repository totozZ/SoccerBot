#if UNITY_EDITOR
using NUnit.Framework;
using SoccerBot;
using UnityEngine;

namespace SoccerBot.Tests
{
    public class FieldAIControllerTests
    {
        [Test]
        public void ClosestPointOnSegment_ProjectsInsideSegment()
        {
            Vector3 point = FieldAIController.ClosestPointOnSegment(
                new Vector3(0f, 0f, 0f),
                new Vector3(0f, 0f, 10f),
                new Vector3(3f, 0f, 4f));

            Assert.That(point.x, Is.EqualTo(0f).Within(0.0001f));
            Assert.That(point.z, Is.EqualTo(4f).Within(0.0001f));
        }

        [Test]
        public void ClosestPointOnSegment_ClampsPastEnd()
        {
            Vector3 point = FieldAIController.ClosestPointOnSegment(
                new Vector3(0f, 0f, 0f),
                new Vector3(0f, 0f, 10f),
                new Vector3(0f, 0f, 14f));

            Assert.That(point.z, Is.EqualTo(10f).Within(0.0001f));
        }

        [Test]
        public void ClampGoalkeeperLane_ClampsToLaneWidth()
        {
            Vector3 target = FieldAIController.ClampGoalkeeperLane(
                Vector3.zero,
                Vector3.right,
                1.25f,
                new Vector3(3f, 2f, 8f));

            Assert.That(target.x, Is.EqualTo(1.25f).Within(0.0001f));
            Assert.That(target.y, Is.EqualTo(0f).Within(0.0001f));
        }

        [Test]
        public void IsMovingTowardGoal_IgnoresVerticalVelocity()
        {
            bool movingTowardGoal = FieldAIController.IsMovingTowardGoal(
                new Vector3(0f, 5f, 2f),
                Vector3.forward,
                0.25f);

            Assert.That(movingTowardGoal, Is.True);
        }

        [Test]
        public void IsMovingTowardGoal_RejectsSidewaysShot()
        {
            bool movingTowardGoal = FieldAIController.IsMovingTowardGoal(
                Vector3.right,
                Vector3.forward,
                0.25f);

            Assert.That(movingTowardGoal, Is.False);
        }
    }
}
#endif
