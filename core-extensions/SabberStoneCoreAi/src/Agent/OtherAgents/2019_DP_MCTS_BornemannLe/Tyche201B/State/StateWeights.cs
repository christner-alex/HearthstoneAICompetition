using System;
using System.Diagnostics;
using System.Globalization;
using SabberStoneCore.Enums;

namespace SabberStoneCoreAi.Tyche2
{
	public class StateWeights
	{
		public enum WeightType
		{
			EmptyField,
			HealthFactor,
			DeckFactor,
			HandFactor,
			MinionFactor,

			/// <summary> Used for "stuff" that doesn't fit to the other categories e.g. unknown secrets </summary>
			BiasFactor,

			Count
		}

		private readonly float[] _weights;

		public StateWeights()
		{
			_weights = new float[(int) WeightType.Count];
		}

		public StateWeights(float defaultValues)
			: this()
		{
			for (var i = 0; i < _weights.Length; i++)
				_weights[i] = defaultValues;
		}

		public StateWeights(params float[] defaultValues)
			: this()
		{
			DebugUtils.Assert(defaultValues.Length == (int) WeightType.Count);

			for (var i = 0; i < _weights.Length; i++)
				_weights[i] = defaultValues[i];
		}

		public StateWeights(Random random, float minValue, float maxValue)
			: this()
		{
			for (var i = 0; i < _weights.Length; i++)
				_weights[i] = random.RandFloat(minValue, maxValue);
		}

		public StateWeights(StateWeights other)
			: this()
		{
			for (var i = 0; i < _weights.Length; i++)
				_weights[i] = other._weights[i];
		}

		public float GetWeight(WeightType t)
		{
			return _weights[(int) t];
		}

		public void SetWeight(WeightType t, float value)
		{
			_weights[(int) t] = value;
		}

		public void Clamp(float min, float max)
		{
			for (var i = 0; i < _weights.Length; i++)
				_weights[i] = Math.Clamp(_weights[i], min, max);
		}

		public override string ToString()
		{
			return ToCsvString(", ");
		}

		public string ToCsvString(string separator)
		{
			var s = "";

			for (var i = 0; i < _weights.Length; i++)
				s += _weights[i].ToString(CultureInfo.InvariantCulture) + separator;

			return s;
		}

		public static StateWeights UniformRandLerp(StateWeights lhs, StateWeights rhs, Random random, float tMin,
			float tMax)
		{
			var p = new StateWeights();

			for (var i = 0; i < p._weights.Length; i++)
			{
				var t = random.RandFloat(tMin, tMax);
				p._weights[i] = Utils.Lerp(lhs._weights[i], rhs._weights[i], t);
			}

			return p;
		}

		public static StateWeights UniformLerp(StateWeights lhs, StateWeights rhs, float t)
		{
			var p = new StateWeights();

			for (var i = 0; i < p._weights.Length; i++)
				p._weights[i] = Utils.Lerp(lhs._weights[i], rhs._weights[i], t);

			return p;
		}

		public static StateWeights NonUniformLerp(StateWeights lhs, StateWeights rhs, float[] tValues)
		{
			Debug.Assert(tValues.Length >= (int) WeightType.Count);

			var p = new StateWeights();

			for (var i = 0; i < p._weights.Length; i++)
				p._weights[i] = Utils.Lerp(lhs._weights[i], rhs._weights[i], tValues[i]);

			return p;
		}

		public static StateWeights operator *(StateWeights lhs, float rhs)
		{
			var p = new StateWeights();

			for (var i = 0; i < p._weights.Length; i++)
				p._weights[i] = lhs._weights[i] * rhs;

			return p;
		}

		public static StateWeights operator /(StateWeights lhs, float rhs)
		{
			var p = new StateWeights();

			for (var i = 0; i < p._weights.Length; i++)
				p._weights[i] = lhs._weights[i] / rhs;

			return p;
		}

		public static StateWeights operator *(StateWeights lhs, StateWeights rhs)
		{
			var p = new StateWeights();

			for (var i = 0; i < p._weights.Length; i++)
				p._weights[i] = lhs._weights[i] * rhs._weights[i];

			return p;
		}

		public static StateWeights operator /(StateWeights lhs, StateWeights rhs)
		{
			var p = new StateWeights();

			for (var i = 0; i < p._weights.Length; i++)
				p._weights[i] = lhs._weights[i] / rhs._weights[i];

			return p;
		}

		public static StateWeights operator +(StateWeights lhs, StateWeights rhs)
		{
			var p = new StateWeights();

			for (var i = 0; i < p._weights.Length; i++)
				p._weights[i] = lhs._weights[i] + rhs._weights[i];

			return p;
		}

		public static StateWeights operator -(StateWeights lhs, StateWeights rhs)
		{
			var p = new StateWeights();

			for (var i = 0; i < p._weights.Length; i++)
				p._weights[i] = lhs._weights[i] - rhs._weights[i];

			return p;
		}

		public static StateWeights GetDefault()
		{
			var p = new StateWeights(1.0f);
			p.SetWeight(WeightType.HealthFactor, 8.7f);
			return p;
		}

		public static StateWeights GetHeroBased(CardClass myClass, CardClass enemyClass)
		{
			if (myClass == CardClass.WARRIOR)
				return new StateWeights(6.083261f, 3.697277f, 3.603937f, 9.533023f, 8.534495f, 8.220309f);

			if (myClass == CardClass.HUNTER)
				return new StateWeights(7.065110f, 3.697277f, 3.603937f, 9.533023f, 8.534495f, 8.220309f);

			if (myClass == CardClass.SHAMAN)
				return new StateWeights(3.168855f, 5.913401f, 3.937068f, 9.007857f, 8.526226f, 5.678857f);

			if (myClass == CardClass.MAGE)
				return new StateWeights(3.133729f, 9.927018f, 2.963968f, 6.498888f, 4.516192f, 4.645887f);

			if (myClass == CardClass.DRUID)
				return new StateWeights(1.995913f, 4.501529f, 1.888616f, 1.096681f, 3.516505f, 1.0f);

			if (myClass == CardClass.PRIEST)
				return new StateWeights(1.995913f, 4.501529f, 1.888616f, 1.096681f, 3.516505f, 1.0f);

			if (myClass == CardClass.WARLOCK)
				return new StateWeights(6.338876f, 8.568761f, 1.863452f, 3.182807f, 4.967152f, 1.0f);

			if (myClass == CardClass.PALADIN)
				return new StateWeights(6.083261f, 3.697277f, 3.603937f, 9.533023f, 8.534495f, 8.220309f);

			if (myClass == CardClass.ROGUE)
				return new StateWeights(6.083261f, 3.697277f, 3.603937f, 9.533023f, 8.534495f, 8.220309f);

			return GetDefault();
		}
	}
}
