import networkx as nx
import json

class Structure():
    def __init__(self, graph=None, name="Structure", attr={}):
        if graph is None:
            self.graph = nx.Graph(name=name)

            for key in attr.keys():
                self.graph.graph[key] = attr[key]
        else:
            self.graph = graph

        self.last_joint_id = 0

    def write(self, path):
        print(f"Writing to '{path}'...", end='')
        try:
            with open(path, 'w') as file:
                file.write(json.dumps(nx.node_link_data(self.graph), indent=4, sort_keys=True))
                print("OK")
        except Exception as e:
            print(f"FAILED ({e})")

    @staticmethod
    def read(path):
        print(f"Reading '{path}'...", end='')
        try:
            with open(path, 'r') as file:
                s = Structure(nx.node_link_graph(json.loads(file.read())))
                print("OK")
                return s
        except Exception as e:
            print(f"FAILED ({e})")

    def __getitem__(self, key):
        return self.graph.graph[key]

    def __eq__(self, other):
        return nx.utils.misc.graphs_equal(self.graph, other.graph)

    @property
    def nodes(self):
        return self.graph.nodes

    @property
    def edges(self):
        return self.graph.edges

    def keys(self):
        return self.graph.graph.keys()

    def increment_joint_id(self):
        self.last_joint_id += 1
        return f"J{self.last_joint_id:04d}"

    def add_element(self, id, element_data={}):
        if id in self.graph.nodes:
            raise ValueError(f"Id '{id}' already exists!")
        element_data["node_type"] = "element"
        self.graph.add_nodes_from([(id, element_data)])

    def add_elements(self, ids, element_datas=[]):
        for (id, element_data) in zip(ids, element_datas):
            if id in self.graph.nodes:
                raise ValueError(f"Id '{id}' already exists!")
            self.graph.add_nodes_from([(id, element_data)])

    def add_joint(self, id, joint_data={}):
        if id in self.graph.nodes:
            raise ValueError(f"Id '{id}' already exists!")
        joint_data["node_type"] = "joint"
        self.graph.add_nodes_from([(id, joint_data)])

    def join(self, element_ids, id=None):
        if id is None:
            id = self.increment_joint_id()
            while id in self.graph.nodes:
                id = self.increment_joint_id()
        self.add_joint(id, {"joint_valence":f"{len(element_ids)}"})

        for element_id in element_ids:
            self.graph.add_edge(element_id, id)

