#pragma once
#include <msclr\marshal_cppstd.h>

#include "OccNet.h"
#include "NurbsCurve.h"

#include <opencascade/Standard_Handle.hxx>
#include <opencascade/Brep_Builder.hxx>
#include <opencascade/TopoDS_Compound.hxx>
#include <opencascade/TopoDS_Builder.hxx>
#include <opencascade/TopoDS_Edge.hxx>
#include <opencascade/BRepBuilderAPI_MakeEdge.hxx>

#include <opencascade/Geom_BSplineCurve.hxx>

#include <opencascade/TopoDSToStep_Builder.hxx>
#include <opencascade/STEPControl_Writer.hxx>
#include <opencascade/STEPControl_StepModelType.hxx>

using namespace System;

namespace OccNet {

	public ref class StepWriter
	{
	public:

		StepWriter()
		{
			m_compound = new TopoDS_Compound();
			std::cout << m_compound << std::endl;

			BRep_Builder builder;
			builder.MakeCompound(*m_compound);
		}

		~StepWriter()
		{
			if (m_compound != nullptr)
				delete m_compound;
		}

		void AddBrep(Brep^ brep)
		{
			TopoDS_Builder builder;
			for (int i = 0; i < brep->vertices->size(); ++i)
			{
				builder.Add(*m_compound, brep->vertices->at(i));
			}
		}

		void AddCurve(BSplineCurve^ curve)
		{
			BRep_Builder builder;
			TopoDS_Edge edge;

			if (m_compound == nullptr)
				throw std::exception("No compound created.");

			const opencascade::handle<Geom_BSplineCurve> handle(curve->m_curve);
			builder.MakeEdge(edge, handle, 1e-10);
			builder.Add(*m_compound, edge);
		}

		void Write(String^ filepath)
		{
			TopoDSToStep_Builder stepBuilder;
			STEPControl_Writer writer;

			writer.Transfer(*m_compound, STEPControl_StepModelType::STEPControl_AsIs);
			std::string path = msclr::interop::marshal_as<std::string>(filepath);

			writer.Write(path.c_str());
		}

	private:
		TopoDS_Compound* m_compound;

	};
}

