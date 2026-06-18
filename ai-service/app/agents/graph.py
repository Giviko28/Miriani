"""Build the agent graph: router -> conditional edge -> specialized agent -> END.

This realizes the proposal's design: a general routing agent (LangGraph) directs each
incoming request to the correct specialized agent.
"""

from langgraph.graph import END, StateGraph

from app.agents.router import route
from app.agents.specialists import SPECIALISTS
from app.agents.state import AgentState


def _build():
    graph = StateGraph(AgentState)

    graph.add_node("router", route)
    for key, fn in SPECIALISTS.items():
        graph.add_node(key, fn)

    graph.set_entry_point("router")
    # Route to the specialist named by state["route"]; fall back to policy_qa.
    graph.add_conditional_edges(
        "router",
        lambda state: state.get("route", "policy_qa"),
        {key: key for key in SPECIALISTS},
    )
    for key in SPECIALISTS:
        graph.add_edge(key, END)

    return graph.compile()


# Compiled once and reused.
agent_graph = _build()


async def run_agents(
    *, org_id: str, role_level: int, query: str, history: list[dict] | None = None,
    attachment_text: str | None = None, attachment_name: str | None = None,
) -> AgentState:
    """Run a request through the router and the chosen specialized agent."""
    initial: AgentState = {
        "query": query, "org_id": org_id, "role_level": role_level, "history": history or [],
        "attachment_text": attachment_text, "attachment_name": attachment_name,
    }
    return await agent_graph.ainvoke(initial)
