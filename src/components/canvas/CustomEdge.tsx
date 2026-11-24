import React, { useState } from 'react';
import { 
  BaseEdge, 
  EdgeLabelRenderer, 
  getSmoothStepPath
} from '@xyflow/react';
import type { EdgeProps } from '@xyflow/react';
import { X } from 'lucide-react';
import { useWorkflowStore } from '../../hooks/useWorkflowStore';

export const CustomEdge: React.FC<EdgeProps> = ({
  id,
  sourceX,
  sourceY,
  targetX,
  targetY,
  sourcePosition,
  targetPosition,
  style = {},
  markerEnd,
  label
}) => {
  const [edgePath, labelX, labelY] = getSmoothStepPath({
    sourceX,
    sourceY,
    sourcePosition,
    targetX,
    targetY,
    targetPosition,
    borderRadius: 16
  });

  const { onEdgesChange } = useWorkflowStore();
  const [isHovered, setIsHovered] = useState(false);

  const handleDeleteEdge = (e: React.MouseEvent) => {
    e.stopPropagation();
    onEdgesChange([{ id, type: 'remove' }]);
  };

  const edgeStroke = isHovered 
    ? '#2563EB' 
    : (style.stroke as string) || '#94A3B8';

  return (
    <g 
      onMouseEnter={() => setIsHovered(true)} 
      onMouseLeave={() => setIsHovered(false)}
      className="cursor-pointer"
    >
      <BaseEdge 
        path={edgePath} 
        markerEnd={markerEnd} 
        style={{
          ...style,
          stroke: edgeStroke,
          strokeWidth: isHovered ? 3 : (style.strokeWidth || 2),
          transition: 'stroke 0.2s, stroke-width 0.2s'
        }} 
      />
      
      {label && (
        <EdgeLabelRenderer>
          <div
            style={{
              position: 'absolute',
              transform: `translate(-50%, -50%) translate(${labelX}px,${labelY}px)`,
              pointerEvents: 'all',
              zIndex: 10,
            }}
            className={`nodrag nopan flex items-center gap-1.5 px-3 py-1 rounded-full border shadow-md backdrop-blur-md transition-all select-none ${
              isHovered 
                ? 'bg-blue-50 dark:bg-blue-950 border-[#2563EB] text-[#2563EB] dark:text-blue-400 scale-105' 
                : 'bg-white/95 dark:bg-slate-900/95 border-slate-300 dark:border-slate-700 text-[#0F172A] dark:text-slate-100'
            }`}
          >
            <span 
              className="w-2.5 h-2.5 rounded-full shrink-0" 
              style={{ backgroundColor: edgeStroke }} 
            />
            <span className="font-bold text-xs">{label}</span>

            <button
              onClick={handleDeleteEdge}
              className="opacity-0 group-hover:opacity-100 p-0.5 rounded-full hover:bg-rose-100 dark:hover:bg-rose-950/60 text-slate-400 hover:text-[#DC2626] dark:hover:text-rose-400 transition-all cursor-pointer focus:opacity-100"
              title="Delete connection"
              aria-label={`Delete connection ${label}`}
            >
              <X className="w-3 h-3" />
            </button>
          </div>
        </EdgeLabelRenderer>
      )}
    </g>
  );
};
