import gmsh
import math
import logging
import struct

try:
    from .classes import Board, Log
except:
    print("Defaulting to direct import...")
    from classes import Board, Log

class BoardGenerator():
    def __init__(self, board, log, element_order = 2):
        self.board = board
        self.log = log
        self.element_order = element_order
        self.tetraType = 4

        gmsh.initialize()

    def generate_model(self):

        gmsh.model.add("BoardGenerator")

        pos = self.board.basePlane.origin
        dims = self.board.dimensions
        logging.debug(f"pos {pos}")
        logging.debug(f"dims {dims}")

        board_id = (3, gmsh.model.occ.addBox(pos[0], pos[1], pos[2], dims[0], dims[1], dims[2]))
        logging.debug(f"board_id {board_id}")

        knot_ids = []

        for i in range(len(self.log.knots)):

            knot = self.log.knots[i]
            cone_id = gmsh.model.occ.addCone(
                x=knot.start[0], 
                y=knot.start[1], 
                z=knot.start[2], 
                dx=knot.direction[0], 
                dy=knot.direction[1], 
                dz=knot.direction[2], 
                r1=0, 
                r2=knot.radius)

            knot_ids.append((3, cone_id))

        trimmed_knots = gmsh.model.occ.intersect([board_id], knot_ids, -1, False, False)
        trimmed_board = gmsh.model.occ.cut([board_id], knot_ids, -1, True, True)
        gmsh.model.occ.synchronize()

        logging.debug(f"trimmed knots {trimmed_knots[1]}")
        logging.debug(f"num original knots: {len(self.log.knots)}")

        # Remap trimmed knots to original knots
        tk_map = trimmed_knots[1][1:]
        assert len(tk_map) == len(knot_ids)
        N = len(tk_map)
        logging.debug(f"tk_map {tk_map}")

        trimmed_knot_names = []

        for i in range(N):
            if len(tk_map[i]) < 1:
                continue
            logging.debug(f"    tk_map {i} : {tk_map[i]}")
            logging.debug(f"        dim {tk_map[i][0][0]}")
            logging.debug(f"        id  {[x[1] for x in tk_map[i]]}")
            gmsh.model.add_physical_group(tk_map[i][0][0], [x[1] for x in tk_map[i]], name=self.log.knots[i].id)

        logging.debug(f"trimmed board {trimmed_board}")
        logging.debug(f"        dim  {trimmed_board[0][0][0]}")
        logging.debug(f"        tags {trimmed_board[0][0][1]}")

        gmsh.model.add_physical_group(trimmed_board[0][0][0], [trimmed_board[0][0][1]], name=self.board.id)


        fragmented_board = gmsh.model.occ.fragment(trimmed_knots[0], trimmed_board[0])[0]

        gmsh.model.occ.synchronize()

        #gmsh.model.add_physical_group(3, [x[1] for x in b0[0]], name="B00")


        return trimmed_board[0], trimmed_knots[0]

    def mesh_model(self):
        # --------- MESHING ---------

        if self.element_order == 2:
            self.tetraType = 11
        gmsh.option.set_number('Mesh.StlOneSolidPerSurface', 2)
        gmsh.option.set_number("Mesh.MeshSizeMin", 3)
        gmsh.option.set_number("Mesh.MeshSizeMax", 50)
        gmsh.option.set_number("Mesh.ElementOrder", self.element_order)
        gmsh.option.set_number("Mesh.SaveAll", 1)
        gmsh.model.occ.synchronize()

        gmsh.model.mesh.generate(3)

    def generate_2d_entities(self):
        entities2d = gmsh.model.get_entities(2)

        for e2d in entities2d:
            logging.debug(f"    e2d {e2d}")

        gmsh.model.addPhysicalGroup(2, [e[1] for e in entities2d], name="Shell")

    def write_integration_points(self, filepath, binary=False):
        # Get all element types in model    
        elementTypes = gmsh.model.mesh.getElementTypes()
        logging.debug(f"elementTypes {elementTypes}, {len(elementTypes)}")

        # Element types:
        # 1  : 2-node line
        # 2  : 3-node triangle
        # 3  : 4-node quadrangle   
        # 4  : 4-node tetrahedron  
        # 5  : 8-node hexahedron   
        # 6  : 6-node prism    
        # 7  : 5-node pyramid  
        # 8  : 3-node second order line
        # 9  : 6-node second order triangle 
        # 10 : 9-node second order quadrangle
        # 11 : 10-node second order tetrahedron

        # Local coordinates and weights of integration points for element type
        localCoords, weights =\
            gmsh.model.mesh.getIntegrationPoints(self.tetraType, "Gauss" + str(self.element_order))
        logging.debug(f"localCoords {localCoords}")

        # Get all 3d elements
        eTypes, eTags, nTags = gmsh.model.mesh.get_elements(dim=3)
        logging.debug(f"eTypes {eTypes}")

        # Get all jacobians and integration points for 3d elements
        jacobians, determinants, coords = gmsh.model.mesh.get_jacobians(eTypes[0], localCoords)

        # Write all integration points to file
        step = len(localCoords)
        N = len(eTags[0])

        if binary:
            with open(filepath, 'wb') as file:
                logging.debug(f"N is {N}")
                logging.debug(f"{eTags}")
                ptr = 0
                for i in range(N):
                    #logging.debug(f"{coords[ptr:ptr + step]}")
                    file.write(struct.pack(f"!l{step}d", eTags[0][i], *coords[ptr:ptr + step]))

                    ptr += step
                pass
            pass
        else:
            with open(filepath, 'w') as file:
                for i in range(N):
                    file.write(f"{eTags[0][i]} ")
                    tok = [f"{coords[i * step + j]}" for j in range(step)]
                    file.write(" ".join(tok))
                    file.write("\n")
#                for i in range(N):
#                    file.write(f"{coords[i * 3 + 0]} {coords[i * 3 + 1]} {coords[i * 3 + 2]}\n")

    def write_nodes(self, filepath, binary=False):
        # Get all node data
        nodeTags, coord, paramatericCoord = gmsh.model.mesh.get_nodes(dim=3, includeBoundary=True, returnParametricCoord=True)

        # Write node data to file
        if binary:
            pass
        else:
            with open(filepath, 'w') as file:
                N = len(coord) // 3
                for i in range(N):
                    file.write(f"{coord[i * 3 + 0]} {coord[i * 3 + 1]} {coord[i * 3 + 2]}\n")

def main():
    logging.basicConfig(level=logging.DEBUG)

    import os
    os.chdir("../../../../examples/data")
    print(os.getcwd())

    board_data = Board.parse_file("sample_board.json")
    log_data = Log.parse_file("sample_log.json")

    bg = BoardGenerator(board_data, log_data)
    bg.generate_model()
    bg.generate_2d_entities()

    gmsh.write("sample_brep.stp")
    bg.mesh_model()

    bg.write_integration_points("sample_integration_points.bin", binary=True)
    bg.write_integration_points("sample_integration_points.xyz", binary=False)

    gmsh.write("sample_mesh.stl")

    gmsh.finalize()

if __name__=="__main__":
    main()




