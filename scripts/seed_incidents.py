"""Seed realistic Silknet IT incident data into the demo SQLite database."""
import sqlite3, json
from pathlib import Path

DB = Path("C:/Users/Giviko/Desktop/sabakalavro/data/demo.db")
DB.parent.mkdir(parents=True, exist_ok=True)

conn = sqlite3.connect(DB)
cur = conn.cursor()

cur.execute("""
CREATE TABLE IF NOT EXISTS incidents (
    id             INTEGER PRIMARY KEY,
    incident_id    TEXT NOT NULL,
    title          TEXT NOT NULL,
    severity       TEXT NOT NULL,
    status         TEXT NOT NULL,
    created_at     TEXT NOT NULL,
    resolved_at    TEXT,
    assigned_team  TEXT,
    affected_service TEXT,
    affected_client  TEXT,
    description    TEXT,
    resolution     TEXT
)
""")

ROWS = [
    (1,"INC-2026-001","BGP route flap — partial internet outage for enterprise clients","P1","Resolved",
     "2026-06-05 02:14","2026-06-05 04:38","NOC / Core Network","DIA Internet",
     "TBC Bank, BOG, Aversi Pharma",
     "BGP session with upstream AS174 (Cogent) dropped, triggering a route flap. Three enterprise DIA clients lost internet for 2h 24min.",
     "Re-established BGP session, applied route dampening. Root cause: misconfigured keepalive timers after firmware update."),

    (2,"INC-2026-002","Fiber cut — Rustaveli Ave affecting B2B clients","P2","Resolved",
     "2026-06-06 09:45","2026-06-06 14:20","Field Operations","Fiber (DIA / MPLS)",
     "8 B2B clients on Rustaveli corridor",
     "Physical fiber cut on Rustaveli Ave underground duct (km 3.2). Loss of service for 8 B2B clients including Geocell regional HQ.",
     "Emergency splice completed. Temporary bypass via ring protection active within 2h, permanent splice at 14:20."),

    (3,"INC-2026-003","MPLS latency spike — National Bank of Georgia circuit","P3","Resolved",
     "2026-06-07 11:30","2026-06-07 13:10","Core Network","MPLS L3VPN",
     "National Bank of Georgia",
     "Client reported MPLS latency spiking from <10ms to 180ms. Affecting online banking transaction processing.",
     "QoS queue saturation on PE router P-TBS-03. Reclassified DSCP EF traffic to correct queue. Latency normalized."),

    (4,"INC-2026-004","VPN misconfiguration — new client G&T Co.","P4","Resolved",
     "2026-06-08 15:00","2026-06-08 16:45","Customer Engineering","IPSec VPN",
     "G&T Co. (new client)",
     "Newly provisioned IPSec VPN tunnel failing to establish due to Phase 2 transform mismatch.",
     "Corrected encryption policy from AES-128 to AES-256 per client CPE requirement. Tunnel up, traffic confirmed."),

    (5,"INC-2026-005","Colocation power test — unplanned interruption in rack B3","P2","Resolved",
     "2026-06-09 03:00","2026-06-09 03:18","DC Operations","Colocation Power",
     "Rack B3 tenants (3 clients)",
     "UPS bypass test in DC1 Hall B caused 18-minute unplanned outage for rack B3. ATS failed to transfer within spec.",
     "Loads transferred to generator feed. ATS unit B3-02 scheduled for replacement. SLA credits calculated."),

    (6,"INC-2026-006","DDoS attack on MSSP gateway — 40 Gbps volumetric","P1","Resolved",
     "2026-06-10 18:22","2026-06-10 21:05","Security / NOC","MSSP / DDoS Mitigation",
     "Silknet Enterprise Cloud clients (12 affected)",
     "40 Gbps volumetric DDoS (UDP flood + DNS amplification) targeting MSSP scrubbing gateway. 12 enterprise cloud clients impacted.",
     "Traffic redirected to Arbor scrubbing cluster within 8 min. Blackhole routing applied to attacking /24 prefixes. Post-incident report sent."),

    (7,"INC-2026-007","DNS resolution failure — MagtiCom roaming interconnect","P3","Resolved",
     "2026-06-11 08:10","2026-06-11 10:25","Core Network","Roaming / DNS",
     "MagtiCom (interconnect partner)",
     "DNS resolution failures on GRX interconnect with MagtiCom causing GTP tunnel failures for inbound roaming subscribers.",
     "DNS forwarder rules corrected on GRX PE router. Stale ACL was blocking recursive queries. Resolved after ACL flush."),

    (8,"INC-2026-008","SSL certificate expiry — client portal","P4","Resolved",
     "2026-06-12 09:00","2026-06-12 11:30","Customer Engineering","Client Portal",
     "All portal users",
     "SSL cert for clients.silknet.ge expired. Browser security warnings blocking client portal access.",
     "Certificate renewed and deployed to load balancer. Monitoring alert added 30 days before next expiry."),

    (9,"INC-2026-009","Fiber splice failure — Tbilisi–Kutaisi backbone","P2","Resolved",
     "2026-06-13 06:30","2026-06-13 09:55","Field Operations / NOC","Backbone Fiber (STM-16)",
     "Backbone (no client outage — APS failover active)",
     "Degraded optical power on STM-16 span TBS-KUT-02. APS triggered failover to backup span. Moisture ingress found at junction K-41.",
     "Re-spliced affected segment. Protection reverted to primary at 09:55. Moisture sealing applied."),

    (10,"INC-2026-010","BGP peering session flap — upstream AS174 (second event)","P3","Resolved",
     "2026-06-13 22:40","2026-06-13 23:18","NOC","DIA Internet (upstream)",
     "No direct client impact (ECMP failover handled it)",
     "Second BGP flap with AS174 within 7 days. Session bounced 3 times in 38 min. ECMP to AS1299 prevented client impact.",
     "Route dampening and BFD tuning applied. Escalated to Cogent for upstream investigation. BGP config change freeze in place."),

    (11,"INC-2026-011","DC1 power outage — UPS failover failure, 3 Platinum racks","P1","Resolved",
     "2026-06-14 03:47","2026-06-14 07:30","DC Operations / NOC","Colocation Power / UPS",
     "Liberty Bank (rack D4), Silknet Systems (rack D5), TBC Capital (rack D6)",
     "DC1 Hall D power failure. UPS D-UPS-01 failed to transfer. Generator started but ATS switch D-04 did not close. 3 Platinum SLA racks without power for 3h 43min.",
     "Manual generator tie-in at 05:12. UPS D-UPS-01 replaced under emergency maintenance. SLA credit reports prepared. RCA: failed contactor in ATS D-04."),

    (12,"INC-2026-012","MPLS QoS misconfiguration — Bank of Georgia voice traffic","P2","Resolved",
     "2026-06-15 10:15","2026-06-15 12:50","Core Network","MPLS L3VPN / QoS",
     "Bank of Georgia",
     "Voice call quality degraded (jitter >50ms, packet loss >2%). DSCP EF traffic being treated as best-effort after maintenance window.",
     "QoS policy re-applied on PE routers P-TBS-01 and P-TBS-02. QoS maps were not restored after interface config restore. Process updated."),

    (13,"INC-2026-013","SSL renewal automation failure — secondary CDN endpoint","P4","Resolved",
     "2026-06-15 14:00","2026-06-15 15:20","Customer Engineering","CDN / SSL",
     "Secondary CDN clients (5 clients)",
     "Auto-renewal script for cdn2.silknet.ge failed silently. Certificate expired. Low impact — primary CDN handled traffic.",
     "Certificate renewed manually. Fixed cron job and added Slack alert for renewal failures."),

    (14,"INC-2026-014","Packet loss on 10Gbps uplink — Sunrise Communications peering","P3","Resolved",
     "2026-06-16 07:00","2026-06-16 08:45","NOC / Core Network","Internet Peering (10Gbps)",
     "Partial impact on IXP-destined traffic",
     "1-3% packet loss on 10Gbps peering link to Sunrise at Tbilisi IXP.",
     "Faulty SFP+ module identified and hot-swapped. Traffic normalized. Module sent for RMA."),

    (15,"INC-2026-015","OSPF adjacency loss — backbone router R05","P2","In Progress",
     "2026-06-16 23:15",None,"NOC / Core Network","Backbone OSPF",
     "Indirect — 2 downstream enterprise rings affected",
     "OSPF adjacency between R05 and R06 lost intermittently. Ring protection keeping clients up but redundancy reduced. Root cause under investigation.",
     None),

    (16,"INC-2026-016","New circuit provisioning — Tbilisi Mall 1Gbps DIA","P4","In Progress",
     "2026-06-17 09:00",None,"Customer Engineering / Field Ops","DIA Internet (new circuit)",
     "Tbilisi Mall (new client)",
     "Last-mile fiber installation delayed. CPE delivered but fiber pull requires permit from mall management. Rescheduled to 2026-06-21.",
     None),

    (17,"INC-2026-017","Critical fiber cut — Carrefour & Liberty Bank 10Gbps DIA","P1","In Progress",
     "2026-06-17 14:30",None,"NOC / Field Operations","DIA Fiber (10Gbps)",
     "Carrefour Georgia, Liberty Bank HQ",
     "Fiber duct damaged by construction near Vake Park. Two 10Gbps DIA circuits down. Field crew dispatched. Estimated repair 6-8 hours.",
     None),

    (18,"INC-2026-018","High CPU alert — edge router R07","P3","In Progress",
     "2026-06-18 06:45",None,"NOC","Edge Router R07",
     "No client impact yet",
     "Edge router R07 CPU at 92% for >30 minutes. Likely BGP table walk after partial reset. Traffic handled but redundancy margin reduced.",
     None),

    (19,"INC-2026-019","Broadcast storm — VLAN misconfiguration in colo zone C","P2","Open",
     "2026-06-18 19:30",None,"DC Operations","Colocation LAN (Zone C)",
     "3 colo clients in Zone C",
     "Broadcast storm in VLAN 200 causing link saturation on zone C switches. STP root bridge election failure suspected.",
     None),

    (20,"INC-2026-020","Scheduled maintenance — core router firmware upgrade R01-R03","P4","Open",
     "2026-06-19 02:00",None,"NOC / Core Network","Core Routers (R01, R02, R03)",
     "All clients (maintenance window — no downtime expected)",
     "Firmware upgrade during 02:00-06:00 window. Redundant paths verified. Client notifications sent per SLA maintenance notice requirements.",
     None),
]

cur.executemany(
    "INSERT OR REPLACE INTO incidents VALUES (?,?,?,?,?,?,?,?,?,?,?,?)",
    ROWS,
)
conn.commit()
conn.close()
print(f"Done — {len(ROWS)} incidents written to {DB}")
