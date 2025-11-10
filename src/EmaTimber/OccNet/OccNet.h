#pragma once

#include <vector>

#include <opencascade/gp_Ax2.hxx>
#include <opencascade/gp_Pnt.hxx>
#include <opencascade/gp_Pln.hxx>
#include <opencascade/gp_Circ.hxx>
#include <opencascade/gp_Ax3.hxx>

#include <opencascade/BRep_Builder.hxx>
#include <opencascade/BRepBuilderAPI_MakeVertex.hxx>
#include <opencascade/BRepBuilderAPI_MakeEdge.hxx>
#include <opencascade/BRepBuilderAPI_MakeWire.hxx>
#include <opencascade/BRepBuilderAPI_MakeFace.hxx>
#include <opencascade/TopoDS_Vertex.hxx>
#include <opencascade/TopoDS_Edge.hxx>
#include <opencascade/TopoDS_Wire.hxx>
#include <opencascade/TopoDS_Face.hxx>

#include "NurbsCurve.h"

using namespace System;

namespace OccNet {

	public ref class Circle
	{
	public:
		Circle(array<double>^ center, array<double>^ normal, array<double>^ xAxis, double radius)
		{
			gp_Ax2 plane(
				gp_Pnt(center[0], center[1], center[2]),
				gp_Dir(normal[0], normal[1], normal[2]),
				gp_Dir(xAxis[0], xAxis[1], xAxis[2])
			);

			m_circle = new gp_Circ(plane, radius);
		}

		~Circle()
		{
			if (m_circle != nullptr)
				delete m_circle;
		}

		gp_Circ* m_circle;
	};



	public ref class Brep
	{
	public:

		Brep()
		{
			vertices = new std::vector<TopoDS_Vertex>();
		}

		~Brep()
		{
			if (vertices != nullptr)
				delete vertices;
			if (edges != nullptr)
				delete edges;
			if (faces != nullptr)
				delete faces;
		}

		void AddVertex(double x, double y, double z)
		{

			TopoDS_Vertex vert = BRepBuilderAPI_MakeVertex(gp_Pnt(x, y, z));
			if (vert.IsNull())
				throw std::exception("Failed to make vertex.");
			//BRepBuilderAPI_MakeVertex makeVert(gp_Pnt(x, y, z));
			//if (!makeVert.IsDone())
			//	throw std::exception("Failed to make vertex.");
			//vertices->push_back(makeVert.Vertex());		

			vertices->push_back(vert);
		}

		void AddEdge(BSplineCurve^ curve)
		{
		}

		void AddSurface()
		{
		}

		void Sample()
		{
			//Init brep builder utility
			BRep_Builder aBuilder;
			//Creation of an inifite face lying on a plane
			gp_Pln planeXY;
			TopoDS_Face aFace = BRepBuilderAPI_MakeFace(planeXY);
			//Crating tow wires to bound the face
			gp_Ax2 Ax2(gp_Pnt(), gp_Dir(0, 0, 1), gp_Dir(1, 0, 0));
			TopoDS_Wire wireIn = BRepBuilderAPI_MakeWire(BRepBuilderAPI_MakeEdge(gp_Circ(Ax2, 1)));
			TopoDS_Wire wireOut = BRepBuilderAPI_MakeWire(BRepBuilderAPI_MakeEdge(gp_Circ(Ax2, 2)));
			//Add outer bound to the face
			aBuilder.Add(aFace, wireOut);
			//Add inner bound. Must be reversed
			aBuilder.Add(aFace, wireIn.Reversed());
			//Add more inner boundaries
			int nCuts = 30;
			for (int i = 0; i < nCuts; i++) {
				gp_Ax2 Ax(gp_Pnt(1.5, 0, 0), gp_Dir(0, 0, 1), gp_Dir(1, 0, 0));
				TopoDS_Wire wire = BRepBuilderAPI_MakeWire(BRepBuilderAPI_MakeEdge(gp_Circ(Ax, 0.1)));
				gp_Trsf rot;
				rot.SetRotation(gp_Ax1(gp_Pnt(), gp_Dir(0, 0, 1)), 2. * M_PI * i / (nCuts - 1.));
				wire.Move(rot);
				aBuilder.Add(aFace, wire.Reversed());
			}
		}

		std::vector<TopoDS_Vertex>* vertices;
		std::vector<TopoDS_Edge>* edges;
		std::vector<TopoDS_Face>* faces;
	};
}
