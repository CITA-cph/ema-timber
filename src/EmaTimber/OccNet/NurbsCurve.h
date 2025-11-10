#pragma once
#include <vector>

#include <opencascade/Geom_BSplineCurve.hxx>
#include <opencascade/TColStd_Array1OfInteger.hxx>
#include <opencascade/TColStd_Array1OfReal.hxx>
#include <opencascade/TColgp_Array1OfPnt.hxx>

using System::IntPtr;
using System::Runtime::InteropServices::Marshal;

namespace OccNet {
    public ref class BSplineCurve
    {


    public:
        BSplineCurve(array<double>^ points, array<double>^ weights, array<double>^ knots, int degree, bool periodic)
        {
            size_t nPoints = points->Length / 3;

            TColgp_Array1OfPnt polesArray(1, nPoints);
            for (int i = 0; i < nPoints; ++i)
                polesArray.SetValue(i + 1, gp_Pnt(points[i * 3], points[i * 3 + 1], points[i * 3 + 2]));

            TColStd_Array1OfReal weightsArray(1, weights->Length);
            for (int i = 0; i < weights->Length; ++i)
            {
                double weight = weights[i];
                weightsArray.SetValue(i + 1, weight);
            }

            array<double>^ knotsNew;
            array<int>^ multiplicities;

            ConvertKnots(knots, degree, knotsNew, multiplicities);

            TColStd_Array1OfReal knotsArray(1, knotsNew->Length);
            for (int i = 0; i < knotsNew->Length; ++i)
            {
                double knot = knotsNew[i];
                knotsArray.SetValue(i + 1, knot);
            }

            TColStd_Array1OfInteger multiplicitiesArray(1, multiplicities->Length);
            for (int i = 0; i < multiplicities->Length; ++i)
            {
                double multiplicity = multiplicities[i];
                multiplicitiesArray.SetValue(i + 1, multiplicity);
            }

            m_curve = new Geom_BSplineCurve(polesArray, weightsArray, knotsArray, multiplicitiesArray, degree, periodic);
        }

        ~BSplineCurve()
        {
            if (m_curve != nullptr)
                delete m_curve;
        }

    internal:
        static void ConvertKnots(array<double>^ knotsOriginal, int degree, [System::Runtime::InteropServices::Out] array<double>^% knotsOCC, [System::Runtime::InteropServices::Out] array<int>^% multiplicitiesOCC)
        {
            double lastKnot = knotsOriginal[0];
            double currentKnot = knotsOriginal[1];
            int mult = 1;

            std::vector<double> knots;
            std::vector<int> mults;

            double epsilon = 1e-6;

            for (int i = 1; i < knotsOriginal->Length; ++i)
            {
                currentKnot = knotsOriginal[i];
                if (abs(lastKnot - currentKnot) > epsilon)
                {
                    knots.push_back(lastKnot);
                    mults.push_back(mult);

                    mult = 1;
                    lastKnot = currentKnot;
                }
                else
                {
                    mult++;
                }
            }

            knots.push_back(currentKnot);
            mults.push_back(mult);

            mults[0] = degree + 1;
            mults[mults.size() - 1] = degree + 1;

            knotsOCC = gcnew array<double>(knots.size());
            Marshal::Copy(IntPtr(knots.data()), knotsOCC, 0, knots.size());

            multiplicitiesOCC = gcnew array<int>(mults.size());
            Marshal::Copy(IntPtr(mults.data()), multiplicitiesOCC, 0, mults.size());
        }

        Geom_BSplineCurve* m_curve;
    };
}
