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

        [Test]
        public void IsShotThreat_RequiresSpeedAndGoalDirection()
        {
            Assert.That(
                FieldAIController.IsShotThreat(new Vector3(0f, 0f, 2f), Vector3.forward, 1.2f, 0.25f),
                Is.True);
            Assert.That(
                FieldAIController.IsShotThreat(new Vector3(0f, 0f, 0.4f), Vector3.forward, 1.2f, 0.25f),
                Is.False);
            Assert.That(
                FieldAIController.IsShotThreat(Vector3.right * 3f, Vector3.forward, 1.2f, 0.25f),
                Is.False);
        }

        [Test]
        public void IsMovingTowardTarget_UsesFlatTargetDirection()
        {
            bool movingToTarget = FieldAIController.IsMovingTowardTarget(
                new Vector3(2f, 0f, 0f),
                Vector3.zero,
                new Vector3(5f, 3f, 0f),
                0.5f,
                0.6f);

            Assert.That(movingToTarget, Is.True);
        }

        [Test]
        public void EvaluatePassPressure_UsesLaneOrReceiverThreat()
        {
            float lanePressure = FieldAIController.EvaluatePassPressure(
                laneDistance: 0.7f,
                receiverDistance: 4f,
                interceptRadius: 0.5f,
                laneRadius: 2.5f,
                receiverRadius: 2f,
                ownershipScale: 1f);
            float receiverPressure = FieldAIController.EvaluatePassPressure(
                laneDistance: 4f,
                receiverDistance: 0.7f,
                interceptRadius: 0.5f,
                laneRadius: 2.5f,
                receiverRadius: 2f,
                ownershipScale: 1f);

            Assert.That(lanePressure, Is.GreaterThan(0.8f));
            Assert.That(receiverPressure, Is.GreaterThan(0.8f));
        }

        [Test]
        public void BuildTeammateSupportTarget_AppliesSideForwardAndHomeY()
        {
            Vector3 target = FieldAIController.BuildTeammateSupportTarget(
                origin: new Vector3(1f, 9f, 2f),
                attackForward: Vector3.forward,
                sideOffset: -2f,
                forwardOffset: 3f,
                targetY: 0.25f);

            Assert.That(target.x, Is.EqualTo(-1f).Within(0.0001f));
            Assert.That(target.y, Is.EqualTo(0.25f).Within(0.0001f));
            Assert.That(target.z, Is.EqualTo(5f).Within(0.0001f));
        }
    }
}
#endif
